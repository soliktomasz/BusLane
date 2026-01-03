namespace BusLane.Services.ServiceBus;

using BusLane.Models;

public interface IServiceBusService
{
    Task<IEnumerable<AzureSubscription>> GetSubscriptionsAsync(CancellationToken ct = default);

    Task<IEnumerable<ServiceBusNamespace>> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default);

    Task<IEnumerable<QueueInfo>> GetQueuesAsync(string namespaceId, CancellationToken ct = default);

    Task<IEnumerable<TopicInfo>> GetTopicsAsync(string namespaceId, CancellationToken ct = default);

    Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string namespaceId, string topicName, CancellationToken ct = default);

    Task<IEnumerable<MessageInfo>> PeekMessagesAsync(string endpoint, string queueOrTopic, string? subscription, int count, bool deadLetter, bool requiresSession = false, CancellationToken ct = default);

    Task SendMessageAsync(string endpoint, string queueOrTopic, string body, IDictionary<string, object>? properties, CancellationToken ct = default);

    Task SendMessageAsync(
        string endpoint, 
        string queueOrTopic, 
        string body, 
        IDictionary<string, object>? properties,
        string? contentType,
        string? correlationId,
        string? messageId,
        string? sessionId,
        string? subject,
        string? to,
        string? replyTo,
        string? replyToSessionId,
        string? partitionKey,
        TimeSpan? timeToLive,
        DateTimeOffset? scheduledEnqueueTime,
        CancellationToken ct = default);

    Task DeleteMessageAsync(string endpoint, string queueOrTopic, string? subscription, long sequenceNumber, CancellationToken ct = default);

    Task PurgeMessagesAsync(string endpoint, string queueOrTopic, string? subscription, bool deadLetter, CancellationToken ct = default);

    /// <summary>
    /// Deletes multiple messages in bulk by their sequence numbers.
    /// </summary>
    Task<int> DeleteMessagesAsync(string endpoint, string queueOrTopic, string? subscription, IEnumerable<long> sequenceNumbers, bool deadLetter = false, CancellationToken ct = default);

    /// <summary>
    /// Resends multiple messages in bulk to the specified entity.
    /// </summary>
    Task<int> ResendMessagesAsync(string endpoint, string queueOrTopic, IEnumerable<MessageInfo> messages, CancellationToken ct = default);

    /// <summary>
    /// Moves messages from dead letter queue back to the main queue by resending them.
    /// </summary>
    Task<int> ResubmitDeadLetterMessagesAsync(string endpoint, string queueOrTopic, string? subscription, IEnumerable<MessageInfo> messages, CancellationToken ct = default);
}
