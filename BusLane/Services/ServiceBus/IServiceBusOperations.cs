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

    // Message operations
    Task<IEnumerable<MessageInfo>> PeekMessagesAsync(
        string entityName,
        string? subscription,
        int count,
        long? fromSequenceNumber,
        bool deadLetter,
        bool requiresSession = false,
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

    Task<int> DeleteMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool deadLetter = false,
        CancellationToken ct = default);

    Task<int> ResendMessagesAsync(
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default);

    Task<int> ResubmitDeadLetterMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default);
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
