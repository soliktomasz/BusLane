namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using BusLane.Models;

/// <summary>
/// Unified interface for all Service Bus operations.
/// Implementations handle either connection string or Azure credential authentication.
/// Implements IAsyncDisposable to properly clean up ServiceBusClient resources.
/// </summary>
public interface IServiceBusOperations : IAsyncDisposable
{
    /// <summary>
    /// Gets the underlying ServiceBusClient for this operations instance.
    /// Used for live streaming scenarios requiring direct access to the client.
    /// </summary>
    ServiceBusClient GetClient();

    // Entity discovery
    Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken ct = default);
    Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken ct = default);
    Task<IEnumerable<TopicInfo>> GetTopicsAsync(CancellationToken ct = default);
    Task<TopicInfo?> GetTopicInfoAsync(string topicName, CancellationToken ct = default);
    Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default);

    /// <summary>
    /// Creates a queue.
    /// </summary>
    /// <param name="options">Queue creation options.</param>
    /// <param name="ct">Cancellation token for the create operation.</param>
    Task CreateQueueAsync(QueueCreationOptions options, CancellationToken ct = default);

    /// <summary>
    /// Creates a topic.
    /// </summary>
    /// <param name="options">Topic creation options.</param>
    /// <param name="ct">Cancellation token for the create operation.</param>
    Task CreateTopicAsync(TopicCreationOptions options, CancellationToken ct = default);

    /// <summary>
    /// Creates a subscription on the specified topic.
    /// </summary>
    /// <param name="topicName">Name of the topic that will own the subscription.</param>
    /// <param name="options">Subscription creation options, including name and session requirement.</param>
    /// <param name="ct">Cancellation token for the create operation.</param>
    Task CreateSubscriptionAsync(
        string topicName,
        SubscriptionCreationOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a subscription from the specified topic.
    /// </summary>
    /// <param name="topicName">Name of the topic that owns the subscription.</param>
    /// <param name="subscriptionName">Name of the subscription to delete.</param>
    /// <param name="ct">Cancellation token for the delete operation.</param>
    Task DeleteSubscriptionAsync(
        string topicName,
        string subscriptionName,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a queue by name.
    /// </summary>
    /// <param name="queueName">Name of the queue to delete.</param>
    /// <param name="ct">Cancellation token for the delete operation.</param>
    Task DeleteQueueAsync(string queueName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a topic by name.
    /// </summary>
    /// <param name="topicName">Name of the topic to delete.</param>
    /// <param name="ct">Cancellation token for the delete operation.</param>
    Task DeleteTopicAsync(string topicName, CancellationToken ct = default);

    /// <summary>
    /// Updates queue properties.
    /// </summary>
    /// <param name="queueName">Name of the queue to update.</param>
    /// <param name="options">Queue update options.</param>
    /// <param name="ct">Cancellation token for the update operation.</param>
    Task UpdateQueueAsync(string queueName, QueueUpdateOptions options, CancellationToken ct = default);

    /// <summary>
    /// Updates topic properties.
    /// </summary>
    /// <param name="topicName">Name of the topic to update.</param>
    /// <param name="options">Topic update options.</param>
    /// <param name="ct">Cancellation token for the update operation.</param>
    Task UpdateTopicAsync(string topicName, TopicUpdateOptions options, CancellationToken ct = default);

    /// <summary>
    /// Updates subscription properties.
    /// </summary>
    /// <param name="topicName">Name of the topic that owns the subscription.</param>
    /// <param name="subscriptionName">Name of the subscription to update.</param>
    /// <param name="options">Subscription update options.</param>
    /// <param name="ct">Cancellation token for the update operation.</param>
    Task UpdateSubscriptionAsync(string topicName, string subscriptionName, SubscriptionUpdateOptions options, CancellationToken ct = default);

    /// <summary>
    /// Gets rules for a subscription.
    /// </summary>
    /// <param name="topicName">Name of the topic that owns the subscription.</param>
    /// <param name="subscriptionName">Name of the subscription whose rules should be loaded.</param>
    /// <param name="ct">Cancellation token for the load operation.</param>
    Task<IReadOnlyList<SubscriptionRuleInfo>> GetSubscriptionRulesAsync(string topicName, string subscriptionName, CancellationToken ct = default);

    /// <summary>
    /// Creates a rule on the specified subscription.
    /// </summary>
    /// <param name="topicName">Name of the topic that owns the subscription.</param>
    /// <param name="subscriptionName">Name of the subscription that will own the rule.</param>
    /// <param name="options">Subscription rule creation options.</param>
    /// <param name="ct">Cancellation token for the create operation.</param>
    Task CreateSubscriptionRuleAsync(string topicName, string subscriptionName, SubscriptionRuleCreationOptions options, CancellationToken ct = default);

    /// <summary>
    /// Deletes a rule from the specified subscription.
    /// </summary>
    /// <param name="topicName">Name of the topic that owns the subscription.</param>
    /// <param name="subscriptionName">Name of the subscription that owns the rule.</param>
    /// <param name="ruleName">Name of the rule to delete.</param>
    /// <param name="ct">Cancellation token for the delete operation.</param>
    Task DeleteSubscriptionRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken ct = default);

    // Message operations
    Task<IEnumerable<MessageInfo>> PeekMessagesAsync(
        string entityName,
        string? subscription,
        int count,
        long? fromSequenceNumber,
        bool deadLetter,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<SessionInspectorItem>> GetSessionInspectorItemsAsync(
        string entityName,
        string? subscription,
        CancellationToken ct = default);

    Task SendMessageAsync(
        string entityName,
        string body,
        IDictionary<string, object>? properties,
        string? contentType = null,
        string? correlationId = null,
        string? messageId = null,
        string? sessionId = null,
        string? subject = null,
        string? to = null,
        string? replyTo = null,
        string? replyToSessionId = null,
        string? partitionKey = null,
        TimeSpan? timeToLive = null,
        DateTimeOffset? scheduledEnqueueTime = null,
        CancellationToken ct = default);

    /// <summary>
    /// Schedules a message for future enqueue and returns its scheduled sequence number.
    /// </summary>
    /// <param name="entityName">Queue or topic path that will receive the message.</param>
    /// <param name="body">Message body content.</param>
    /// <param name="properties">Optional application properties.</param>
    /// <param name="scheduledEnqueueTime">UTC time when the message should become active.</param>
    /// <param name="contentType">Optional message content type.</param>
    /// <param name="correlationId">Optional correlation id.</param>
    /// <param name="messageId">Optional message id.</param>
    /// <param name="sessionId">Optional session id.</param>
    /// <param name="subject">Optional message subject.</param>
    /// <param name="to">Optional destination metadata.</param>
    /// <param name="replyTo">Optional reply-to entity.</param>
    /// <param name="replyToSessionId">Optional reply-to session id.</param>
    /// <param name="partitionKey">Optional partition key.</param>
    /// <param name="timeToLive">Optional message TTL.</param>
    /// <param name="ct">Cancellation token for the schedule operation.</param>
    Task<long> ScheduleMessageAsync(
        string entityName,
        string body,
        IDictionary<string, object>? properties,
        DateTimeOffset scheduledEnqueueTime,
        string? contentType = null,
        string? correlationId = null,
        string? messageId = null,
        string? sessionId = null,
        string? subject = null,
        string? to = null,
        string? replyTo = null,
        string? replyToSessionId = null,
        string? partitionKey = null,
        TimeSpan? timeToLive = null,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels a previously scheduled message.
    /// </summary>
    /// <param name="entityName">Queue or topic path that owns the scheduled message.</param>
    /// <param name="sequenceNumber">Scheduled message sequence number returned by <see cref="ScheduleMessageAsync"/>.</param>
    /// <param name="ct">Cancellation token for the cancel operation.</param>
    Task CancelScheduledMessageAsync(
        string entityName,
        long sequenceNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Receives messages in peek-lock mode for later settlement.
    /// </summary>
    /// <param name="entityName">Queue or topic path to receive from.</param>
    /// <param name="subscription">Optional subscription name when receiving from a topic subscription.</param>
    /// <param name="count">Maximum number of messages to receive.</param>
    /// <param name="deadLetter">Whether to receive from the dead-letter subqueue.</param>
    /// <param name="requiresSession">Whether the entity requires sessions.</param>
    /// <param name="sessionId">Optional session id to receive from.</param>
    /// <param name="ct">Cancellation token for the receive operation.</param>
    Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveMessagesAsync(
        string entityName,
        string? subscription,
        int count,
        bool deadLetter,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Receives deferred messages by sequence number in peek-lock mode.
    /// </summary>
    /// <param name="entityName">Queue or topic path to receive from.</param>
    /// <param name="subscription">Optional subscription name when receiving from a topic subscription.</param>
    /// <param name="sequenceNumbers">Deferred message sequence numbers.</param>
    /// <param name="requiresSession">Whether the entity requires sessions.</param>
    /// <param name="sessionId">Session id required for session-enabled deferred messages.</param>
    /// <param name="ct">Cancellation token for the receive operation.</param>
    Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveDeferredMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Completes a received peek-lock message.
    /// </summary>
    /// <param name="message">Message to complete.</param>
    /// <param name="ct">Cancellation token for the settlement operation.</param>
    Task CompleteMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default);

    /// <summary>
    /// Abandons a received peek-lock message.
    /// </summary>
    /// <param name="message">Message to abandon.</param>
    /// <param name="ct">Cancellation token for the settlement operation.</param>
    Task AbandonMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default);

    /// <summary>
    /// Moves a received peek-lock message to the dead-letter queue.
    /// </summary>
    /// <param name="message">Message to dead-letter.</param>
    /// <param name="reason">Optional dead-letter reason.</param>
    /// <param name="description">Optional dead-letter description.</param>
    /// <param name="ct">Cancellation token for the settlement operation.</param>
    Task DeadLetterMessageAsync(ReceivedMessageInfo message, string? reason = null, string? description = null, CancellationToken ct = default);

    /// <summary>
    /// Defers a received peek-lock message.
    /// </summary>
    /// <param name="message">Message to defer.</param>
    /// <param name="ct">Cancellation token for the settlement operation.</param>
    Task DeferMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default);
    /// <summary>
    /// Renews the lock for a peek-lock message and returns the new lock expiration time.
    /// </summary>
    /// <param name="message">Received message whose lock should be renewed.</param>
    /// <param name="ct">Cancellation token for the renew operation.</param>
    Task<DateTimeOffset> RenewMessageLockAsync(ReceivedMessageInfo message, CancellationToken ct = default);

    Task PurgeMessagesAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct = default);

    Task<BulkOperationPreview> PreviewPurgeMessagesAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct = default);

    Task<BulkOperationExecutionResult> PurgeMessagesDetailedAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        IProgress<BulkOperationProgress>? progress = null,
        CancellationToken ct = default);

    Task<int> DeleteMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool deadLetter = false,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default);

    Task<BulkOperationExecutionResult> DeleteMessagesDetailedAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageIdentifier> messages,
        bool deadLetter = false,
        IProgress<BulkOperationProgress>? progress = null,
        bool requiresSession = false,
        CancellationToken ct = default);

    Task<int> ResendMessagesAsync(
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default);

    Task<BulkOperationExecutionResult> ResendMessagesDetailedAsync(
        string entityName,
        IEnumerable<MessageInfo> messages,
        IProgress<BulkOperationProgress>? progress = null,
        CancellationToken ct = default);

    Task<int> ResubmitDeadLetterMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default);

    Task<BulkOperationExecutionResult> ResubmitDeadLetterMessagesDetailedAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        IProgress<BulkOperationProgress>? progress = null,
        bool requiresSession = false,
        CancellationToken ct = default);

    Task<ConnectionHealthReport> CheckConnectionHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Information about a Service Bus namespace.
/// </summary>
public record NamespaceInfo(
    string? Name,
    int QueueCount,
    int TopicCount,
    IReadOnlyList<string> QueueNames,
    IReadOnlyList<string> TopicNames
);

/// <summary>
/// Options used when creating a Service Bus subscription.
/// </summary>
public record SubscriptionCreationOptions(
    string Name,
    bool RequiresSession = false);

/// <summary>
/// Extended operations available only for connection string mode.
/// </summary>
public interface IConnectionStringOperations : IServiceBusOperations
{
    /// <summary>
    /// Gets information about all entities in a namespace.
    /// </summary>
    Task<NamespaceInfo?> GetNamespaceInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates the connection string and returns connection details.
    /// </summary>
    Task<(bool IsValid, string? EntityName, string? Endpoint, string? ErrorMessage)> ValidateAsync(CancellationToken ct = default);
}

/// <summary>
/// Extended operations available only for Azure credential mode.
/// </summary>
public interface IAzureCredentialOperations : IServiceBusOperations
{
    /// <summary>
    /// The namespace resource ID for ARM operations.
    /// </summary>
    string NamespaceId { get; }
}
