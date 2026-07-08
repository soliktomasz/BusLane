namespace BusLane.Services.ServiceBus;

using System.Text;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Azure.Messaging.ServiceBus;
using BusLane.Models;
using BusLane.Services.Abstractions;
using Serilog;

public class LiveStreamService : ILiveStreamService
{
    private Subject<LiveStreamMessage> _messageSubject = new();
    private readonly object _subjectLock = new();
    private readonly IPreferencesService _preferencesService;
    private ServiceBusReceiver? _peekReceiver;
    private CancellationTokenSource? _peekCts;
    private Task? _peekStreamTask;
    private bool _isStreaming;
    private bool _disposed;

    private string _currentEntityName = "";

    private const int DefaultPeekTimeoutSeconds = 30;
    private const int DefaultPollingIntervalSeconds = 1;
    private const int ErrorRetryDelaySeconds = 5;
    private const int GracefulShutdownTimeoutSeconds = 5;
    private const int DefaultPeekBatchSize = 10;
    private const int MaxStreamBodyBytes = 4096;

    public LiveStreamService(IPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
    }

    public IObservable<LiveStreamMessage> Messages
    {
        get
        {
            lock (_subjectLock)
            {
                return _messageSubject.AsObservable();
            }
        }
    }

    public bool IsStreaming => _isStreaming;

    public event EventHandler<bool>? StreamingStatusChanged;
    public event EventHandler<Exception>? StreamError;

    private void EmitMessage(LiveStreamMessage message)
    {
        lock (_subjectLock)
        {
            if (_disposed) return;

            try
            {
                _messageSubject.OnNext(message);
            }
            catch (ObjectDisposedException)
            {
                // Subject was disposed - recreate and notify new subscribers
                _messageSubject = new Subject<LiveStreamMessage>();
                Log.Warning("Live stream subject was disposed, recreated new subject");
            }
        }
    }

    public async Task StartQueueStreamAsync(IServiceBusOperations operations, string queueName, CancellationToken ct = default)
    {
        await StopStreamAsync();

        _currentEntityName = queueName;

        try
        {
            await StartPeekStreamAsync(operations.GetClient(), queueName, null, ct);
        }
        catch (Exception ex)
        {
            StreamError?.Invoke(this, ex);
            throw;
        }
    }

    public async Task StartSubscriptionStreamAsync(IServiceBusOperations operations, string topicName, string subscriptionName, CancellationToken ct = default)
    {
        await StopStreamAsync();

        _currentEntityName = subscriptionName;

        Log.Information("Starting live stream for subscription {TopicName}/{SubscriptionName}",
            topicName, subscriptionName);

        try
        {
            await StartPeekStreamAsync(operations.GetClient(), topicName, subscriptionName, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start live stream for subscription {TopicName}/{SubscriptionName}",
                topicName, subscriptionName);
            StreamError?.Invoke(this, ex);
            throw;
        }
    }

    private Task StartPeekStreamAsync(ServiceBusClient client, string entityName, string? subscriptionName, CancellationToken ct)
    {
        _peekCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var peekCts = _peekCts;

        _peekReceiver = subscriptionName != null
            ? client.CreateReceiver(entityName, subscriptionName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            })
            : client.CreateReceiver(entityName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        var peekReceiver = _peekReceiver;

        long lastSequenceNumber = 0;
        var pollingInterval = TimeSpan.FromSeconds(
            _preferencesService.LiveStreamPollingIntervalSeconds > 0
                ? _preferencesService.LiveStreamPollingIntervalSeconds
                : DefaultPollingIntervalSeconds);

        _peekStreamTask = Task.Run(async () =>
        {
            while (!peekCts.Token.IsCancellationRequested)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultPeekTimeoutSeconds));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        peekCts.Token,
                        timeoutCts.Token
                    );

                    var messages = await peekReceiver.PeekMessagesAsync(DefaultPeekBatchSize, lastSequenceNumber + 1, linkedCts.Token);

                    // Only report "streaming" once the entity has answered a peek,
                    // so bad entity names or missing permissions never show as live.
                    // Skip if stop has begun, so a late peek response cannot flip the status back on.
                    if (!peekCts.Token.IsCancellationRequested)
                    {
                        SetStreamingStatus(true);
                    }

                    foreach (var msg in messages)
                    {
                        if (msg.SequenceNumber > lastSequenceNumber)
                        {
                            lastSequenceNumber = msg.SequenceNumber;

                            var liveMessage = new LiveStreamMessage(
                                msg.MessageId,
                                msg.CorrelationId,
                                msg.ContentType,
                                CreateStreamBody(msg.Body),
                                msg.EnqueuedTime,
                                subscriptionName ?? entityName,
                                subscriptionName != null ? "Subscription" : "Queue",
                                subscriptionName != null ? entityName : null,
                                msg.SequenceNumber,
                                msg.SessionId,
                                msg.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                            );

                            EmitMessage(liveMessage);
                        }
                    }

                    await Task.Delay(pollingInterval, peekCts.Token);
                }
                catch (OperationCanceledException) when (!peekCts.Token.IsCancellationRequested)
                {
                    Log.Debug("Peek timeout, continuing to poll");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException) when (peekCts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in peek stream for {EntityName}", entityName);
                    StreamError?.Invoke(this, ex);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(ErrorRetryDelaySeconds), peekCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }, peekCts.Token);

        return Task.CompletedTask;
    }

    internal static string CreateStreamBody(BinaryData body)
    {
        var memory = body.ToMemory();
        if (memory.Length <= MaxStreamBodyBytes)
        {
            return body.ToString();
        }

        var length = GetUtf8BoundaryLength(memory.Span, MaxStreamBodyBytes);
        return Encoding.UTF8.GetString(memory.Span[..length]) + "...";
    }

    private static int GetUtf8BoundaryLength(ReadOnlySpan<byte> bytes, int maxLength)
    {
        var length = Math.Min(maxLength, bytes.Length);
        var start = length - 1;
        while (start >= 0 && IsUtf8ContinuationByte(bytes[start]))
        {
            start--;
        }

        if (start < 0)
        {
            return 0;
        }

        var expectedLength = GetUtf8SequenceLength(bytes[start]);
        if (expectedLength == 0)
        {
            return start;
        }

        var availableLength = length - start;
        return availableLength >= expectedLength ? length : start;
    }

    private static bool IsUtf8ContinuationByte(byte value) => (value & 0b1100_0000) == 0b1000_0000;

    private static int GetUtf8SequenceLength(byte value)
    {
        if ((value & 0b1000_0000) == 0)
        {
            return 1;
        }

        if ((value & 0b1110_0000) == 0b1100_0000)
        {
            return 2;
        }

        if ((value & 0b1111_0000) == 0b1110_0000)
        {
            return 3;
        }

        return (value & 0b1111_1000) == 0b1111_0000 ? 4 : 0;
    }

    public async Task StopStreamAsync()
    {
        if (_isStreaming)
        {
            Log.Information("Stopping live stream for {EntityName}", _currentEntityName);
        }

        _peekCts?.Cancel();

        if (_peekStreamTask != null)
        {
            try
            {
                await Task.WhenAny(_peekStreamTask, Task.Delay(TimeSpan.FromSeconds(GracefulShutdownTimeoutSeconds)));
                if (!_peekStreamTask.IsCompleted)
                {
                    Log.Warning("Peek stream task did not complete gracefully");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error waiting for peek stream to stop");
            }
            _peekStreamTask = null;
        }

        // Set after the loop task has been awaited so a late peek response cannot flip the status back on.
        SetStreamingStatus(false);

        _peekCts?.Dispose();
        _peekCts = null;

        if (_peekReceiver != null)
        {
            try
            {
                await _peekReceiver.DisposeAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing peek receiver");
            }
            _peekReceiver = null;
        }

        Log.Debug("Live stream resources cleaned up");
    }

    private void SetStreamingStatus(bool isStreaming)
    {
        if (_isStreaming != isStreaming)
        {
            _isStreaming = isStreaming;
            StreamingStatusChanged?.Invoke(this, isStreaming);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopStreamAsync();
        
        lock (_subjectLock)
        {
            _messageSubject.OnCompleted();
            _messageSubject.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}
