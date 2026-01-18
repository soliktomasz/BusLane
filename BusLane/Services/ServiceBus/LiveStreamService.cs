namespace BusLane.Services.ServiceBus;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using BusLane.Models;
using Serilog;

public class LiveStreamService : ILiveStreamService
{
    private Subject<LiveStreamMessage> _messageSubject = new();
    private readonly object _subjectLock = new();
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;
    private ServiceBusReceiver? _peekReceiver;
    private CancellationTokenSource? _peekCts;
    private Task? _peekStreamTask;
    private bool _isStreaming;
    private bool _disposed;

    private string _currentEndpoint = "";
    private string _currentEntityName = "";
    private string? _currentTopicName;
    private bool _isPeekMode = true;

    private const int DefaultPeekTimeoutSeconds = 30;

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
            if (!_disposed)
            {
                try
                {
                    _messageSubject.OnNext(message);
                }
                catch (ObjectDisposedException)
                {
                    // Subject was disposed, recreate it
                    _messageSubject = new Subject<LiveStreamMessage>();
                    _messageSubject.OnNext(message);
                }
            }
        }
    }

    public async Task StartQueueStreamAsync(string endpoint, string queueName, bool peekOnly = true, CancellationToken ct = default)
    {
        await StopStreamAsync();
        
        _currentEndpoint = endpoint;
        _currentEntityName = queueName;
        _currentTopicName = null;
        _isPeekMode = peekOnly;

        try
        {
            _client = new ServiceBusClient(endpoint, new DefaultAzureCredential());
            
            if (peekOnly)
            {
                await StartPeekStreamAsync(queueName, null, ct);
            }
            else
            {
                await StartProcessorStreamAsync(queueName, null);
            }

            SetStreamingStatus(true);
        }
        catch (Exception ex)
        {
            StreamError?.Invoke(this, ex);
            throw;
        }
    }

    public async Task StartSubscriptionStreamAsync(string endpoint, string topicName, string subscriptionName, bool peekOnly = true, CancellationToken ct = default)
    {
        await StopStreamAsync();
        
        _currentEndpoint = endpoint;
        _currentEntityName = subscriptionName;
        _currentTopicName = topicName;
        _isPeekMode = peekOnly;

        Log.Information("Starting live stream for subscription {TopicName}/{SubscriptionName} at {Endpoint} (PeekOnly: {PeekOnly})", 
            topicName, subscriptionName, endpoint, peekOnly);

        try
        {
            _client = new ServiceBusClient(endpoint, new DefaultAzureCredential());
            
            if (peekOnly)
            {
                await StartPeekStreamAsync(topicName, subscriptionName, ct);
            }
            else
            {
                await StartProcessorStreamAsync(topicName, subscriptionName);
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

    private async Task StartPeekStreamAsync(string entityName, string? subscriptionName, CancellationToken ct)
    {
        _peekCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _peekReceiver = subscriptionName != null
            ? _client!.CreateReceiver(entityName, subscriptionName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            })
            : _client!.CreateReceiver(entityName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        long lastSequenceNumber = 0;
        var pollingInterval = TimeSpan.FromSeconds(1);

        try
        {
            _peekStreamTask = Task.Run(async () =>
            {
                while (!_peekCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultPeekTimeoutSeconds));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            _peekCts.Token,
                            timeoutCts.Token
                        );

                        var messages = await _peekReceiver.PeekMessagesAsync(10, lastSequenceNumber + 1, timeoutCts.Token);

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

                        await Task.Delay(pollingInterval, _peekCts.Token);
                    }
                    catch (OperationCanceledException) when (!_peekCts.Token.IsCancellationRequested)
                    {
                        Log.Debug("Peek timeout, continuing to poll");
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in peek stream for {EntityName}", entityName);
                        StreamError?.Invoke(this, ex);
                        await Task.Delay(TimeSpan.FromSeconds(5), _peekCts.Token);
                    }
                }
            }, _peekCts.Token);

            await _peekStreamTask;
        }
        finally
        {
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
        }
    }

    private async Task StartProcessorStreamAsync(string entityName, string? subscriptionName)
    {
        var options = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        _processor = subscriptionName != null
            ? _client!.CreateProcessor(entityName, subscriptionName, options)
            : _client!.CreateProcessor(entityName, options);

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
                await Task.WhenAny(_peekStreamTask, Task.Delay(TimeSpan.FromSeconds(5)));
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

        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
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

