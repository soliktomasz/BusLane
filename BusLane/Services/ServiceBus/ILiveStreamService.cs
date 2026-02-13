namespace BusLane.Services.ServiceBus;

using BusLane.Models;

public interface ILiveStreamService : IAsyncDisposable
{
    /// <summary>
    /// Observable stream of incoming messages
    /// </summary>
    IObservable<LiveStreamMessage> Messages { get; }

    /// <summary>
    /// Whether the stream is currently active
    /// </summary>
    bool IsStreaming { get; }

    /// <summary>
    /// Start streaming messages from a queue using a ServiceBusClient from pooled operations.
    /// This is the recommended method as it uses the connection pool.
    /// </summary>
    Task StartQueueStreamAsync(IServiceBusOperations operations, string queueName, bool peekOnly = true, CancellationToken ct = default);

    /// <summary>
    /// Start streaming messages from a topic subscription using a ServiceBusClient from pooled operations.
    /// This is the recommended method as it uses the connection pool.
    /// </summary>
    Task StartSubscriptionStreamAsync(IServiceBusOperations operations, string topicName, string subscriptionName, bool peekOnly = true, CancellationToken ct = default);

    /// <summary>
    /// Stop the current stream
    /// </summary>
    Task StopStreamAsync();

    /// <summary>
    /// Event raised when streaming status changes
    /// </summary>
    event EventHandler<bool>? StreamingStatusChanged;

    /// <summary>
    /// Event raised when an error occurs during streaming
    /// </summary>
    event EventHandler<Exception>? StreamError;
}
