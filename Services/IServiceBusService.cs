using BusLane.Models;

namespace BusLane.Services;

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
}
