using System.Reactive.Linq;
using System.Reactive.Subjects;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using BusLane.Models;

namespace BusLane.Services;

public class LiveStreamService : ILiveStreamService
{
    private readonly Subject<LiveStreamMessage> _messageSubject = new();
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;
    private ServiceBusReceiver? _peekReceiver;
    private CancellationTokenSource? _peekCts;
    private bool _isStreaming;
    private bool _disposed;
    
    private string _currentEndpoint = "";
    private string _currentEntityName = "";
    private string? _currentTopicName;
    private bool _isPeekMode = true;

    public IObservable<LiveStreamMessage> Messages => _messageSubject.AsObservable();
    
    public bool IsStreaming => _isStreaming;

    public event EventHandler<bool>? StreamingStatusChanged;
    public event EventHandler<Exception>? StreamError;

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
        }
        catch (Exception ex)
        {
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

        // Keep track of last sequence number to avoid duplicates
        long lastSequenceNumber = 0;

        _ = Task.Run(async () =>
        {
            while (!_peekCts.Token.IsCancellationRequested)
            {
                try
                {
                    var messages = await _peekReceiver.PeekMessagesAsync(10, lastSequenceNumber + 1, _peekCts.Token);
                    
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
                            
                            _messageSubject.OnNext(liveMessage);
                        }
                    }
                    
                    // Poll interval
                    await Task.Delay(1000, _peekCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StreamError?.Invoke(this, ex);
                    await Task.Delay(5000, _peekCts.Token); // Backoff on error
                }
            }
        }, _peekCts.Token);
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
            
            _messageSubject.OnNext(liveMessage);
            
            // Abandon message so it can be received again (we're just monitoring)
            await args.AbandonMessageAsync(args.Message);
        };

        _processor.ProcessErrorAsync += args =>
        {
            StreamError?.Invoke(this, args.Exception);
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync();
    }

    public async Task StopStreamAsync()
    {
        SetStreamingStatus(false);
        
        _peekCts?.Cancel();
        _peekCts?.Dispose();
        _peekCts = null;

        if (_peekReceiver != null)
        {
            await _peekReceiver.DisposeAsync();
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
        _messageSubject.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

