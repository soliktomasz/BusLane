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
    /// Streaming is peek-based and never receives messages, so it cannot affect delivery counts.
    /// </summary>
    Task StartQueueStreamAsync(IServiceBusOperations operations, string queueName, CancellationToken ct = default);

    /// <summary>
    /// Start streaming messages from a topic subscription using a ServiceBusClient from pooled operations.
    /// Streaming is peek-based and never receives messages, so it cannot affect delivery counts.
    /// </summary>
    Task StartSubscriptionStreamAsync(IServiceBusOperations operations, string topicName, string subscriptionName, CancellationToken ct = default);

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
