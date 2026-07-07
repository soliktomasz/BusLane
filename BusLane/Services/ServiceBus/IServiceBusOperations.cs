namespace BusLane.Services.ServiceBus;

using BusLane.Models;
using Azure.Messaging.ServiceBus;

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

    Task DeleteQueueAsync(string queueName, CancellationToken ct = default);
    Task DeleteTopicAsync(string topicName, CancellationToken ct = default);
    Task UpdateQueueAsync(string queueName, QueueUpdateOptions options, CancellationToken ct = default);
    Task UpdateTopicAsync(string topicName, TopicUpdateOptions options, CancellationToken ct = default);
    Task UpdateSubscriptionAsync(string topicName, string subscriptionName, SubscriptionUpdateOptions options, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionRuleInfo>> GetSubscriptionRulesAsync(string topicName, string subscriptionName, CancellationToken ct = default);
    Task CreateSubscriptionRuleAsync(string topicName, string subscriptionName, SubscriptionRuleCreationOptions options, CancellationToken ct = default);
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
