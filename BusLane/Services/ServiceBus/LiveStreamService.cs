namespace BusLane.Services.ServiceBus;

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
    private ServiceBusProcessor? _processor;
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

    public async Task StartQueueStreamAsync(IServiceBusOperations operations, string queueName, bool peekOnly = true, CancellationToken ct = default)
    {
        await StopStreamAsync();

        _currentEntityName = queueName;

        try
        {
            if (peekOnly)
            {
                await StartPeekStreamAsync(operations.GetClient(), queueName, null, ct);
            }
            else
            {
                await StartProcessorStreamAsync(operations.GetClient(), queueName, null);
            }

            SetStreamingStatus(true);
        }
        catch (Exception ex)
        {
            StreamError?.Invoke(this, ex);
            throw;
        }
    }

    public async Task StartSubscriptionStreamAsync(IServiceBusOperations operations, string topicName, string subscriptionName, bool peekOnly = true, CancellationToken ct = default)
    {
        await StopStreamAsync();

        _currentEntityName = subscriptionName;

        Log.Information("Starting live stream for subscription {TopicName}/{SubscriptionName} (PeekOnly: {PeekOnly})",
            topicName, subscriptionName, peekOnly);

        try
        {
            if (peekOnly)
            {
                await StartPeekStreamAsync(operations.GetClient(), topicName, subscriptionName, ct);
            }
            else
            {
                await StartProcessorStreamAsync(operations.GetClient(), topicName, subscriptionName);
            }

            SetStreamingStatus(true);
            Log.Debug("Live stream started successfully for subscription {TopicName}/{SubscriptionName}",
                topicName, subscriptionName);
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

                    foreach (var msg in messages)
                    {
                        if (msg.SequenceNumber > lastSequenceNumber)
                        {
                            lastSequenceNumber = msg.SequenceNumber;

                            var liveMessage = new LiveStreamMessage(
                                msg.MessageId,
                                msg.CorrelationId,
                                msg.ContentType,
                                msg.Body.ToString(),
                                DateTimeOffset.UtcNow,
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

    private async Task StartProcessorStreamAsync(ServiceBusClient client, string entityName, string? subscriptionName)
    {
        var options = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        _processor = subscriptionName != null
            ? client.CreateProcessor(entityName, subscriptionName, options)
            : client.CreateProcessor(entityName, options);

        _processor.ProcessMessageAsync += async args =>
        {
            var msg = args.Message;
            
            var liveMessage = new LiveStreamMessage(
                msg.MessageId,
                msg.CorrelationId,
                msg.ContentType,
                msg.Body.ToString(),
                DateTimeOffset.UtcNow,
                subscriptionName ?? entityName,
                subscriptionName != null ? "Subscription" : "Queue",
                subscriptionName != null ? entityName : null,
                msg.SequenceNumber,
                msg.SessionId,
                msg.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            );
            
            EmitMessage(liveMessage);
            
            // Abandon message so it can be received again (we're just monitoring)
            await args.AbandonMessageAsync(args.Message);
        };

        _processor.ProcessErrorAsync += args =>
        {
            Log.Error(args.Exception, "Live stream processor error for entity {EntityPath}", args.EntityPath);
            StreamError?.Invoke(this, args.Exception);
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync();
    }

    public async Task StopStreamAsync()
    {
        if (_isStreaming)
        {
            Log.Information("Stopping live stream for {EntityName}", _currentEntityName);
        }

        SetStreamingStatus(false);

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

        if (_processor != null)
        {
            await _processor.StopProcessingAsync();
            await _processor.DisposeAsync();
            _processor = null;
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
