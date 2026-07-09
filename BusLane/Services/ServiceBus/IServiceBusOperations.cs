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

    Task CancelScheduledMessageAsync(
        string entityName,
        long sequenceNumber,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveMessagesAsync(
        string entityName,
        string? subscription,
        int count,
        bool deadLetter,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveDeferredMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default);

    Task CompleteMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default);
    Task AbandonMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default);
    Task DeadLetterMessageAsync(ReceivedMessageInfo message, string? reason = null, string? description = null, CancellationToken ct = default);
    Task DeferMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default);
    Task RenewMessageLockAsync(ReceivedMessageInfo message, CancellationToken ct = default);

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
        CancellationToken ct = default,
        IProgress<BulkOperationProgress>? progress = null);

    Task<int> DeleteMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool deadLetter = false,
        CancellationToken ct = default);

    Task<BulkOperationExecutionResult> DeleteMessagesDetailedAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageIdentifier> messages,
        bool deadLetter = false,
        CancellationToken ct = default,
        IProgress<BulkOperationProgress>? progress = null);

    Task<int> ResendMessagesAsync(
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default);

    Task<BulkOperationExecutionResult> ResendMessagesDetailedAsync(
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default,
        IProgress<BulkOperationProgress>? progress = null);

    Task<int> ResubmitDeadLetterMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default);

    Task<BulkOperationExecutionResult> ResubmitDeadLetterMessagesDetailedAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default,
        IProgress<BulkOperationProgress>? progress = null);

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
