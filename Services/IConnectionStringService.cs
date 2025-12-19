using BusLane.Models;

namespace BusLane.Services;

/// <summary>
/// Information about a Service Bus namespace
/// </summary>
public record NamespaceInfo(
    string? Name,
    int QueueCount,
    int TopicCount,
    IReadOnlyList<string> QueueNames,
    IReadOnlyList<string> TopicNames
);

public interface IConnectionStringService
{
    Task<IEnumerable<QueueInfo>> GetQueuesFromConnectionAsync(string connectionString, CancellationToken ct = default);
    Task<QueueInfo?> GetQueueInfoAsync(string connectionString, string queueName, CancellationToken ct = default);
    Task<IEnumerable<TopicInfo>> GetTopicsFromConnectionAsync(string connectionString, CancellationToken ct = default);
    Task<TopicInfo?> GetTopicInfoAsync(string connectionString, string topicName, CancellationToken ct = default);
    Task<IEnumerable<SubscriptionInfo>> GetTopicSubscriptionsAsync(string connectionString, string topicName, CancellationToken ct = default);
    
    /// <summary>
    /// Gets information about all entities in a namespace
    /// </summary>
    Task<NamespaceInfo?> GetNamespaceInfoAsync(string connectionString, CancellationToken ct = default);
    
    Task<IEnumerable<MessageInfo>> PeekMessagesAsync(
        string connectionString, 
        string entityName, 
        string? subscription, 
        int count, 
        bool deadLetter, 
        bool requiresSession = false, 
        CancellationToken ct = default);
    
    Task SendMessageAsync(
        string connectionString, 
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
        string connectionString, 
        string entityName, 
        string? subscription, 
        bool deadLetter, 
        CancellationToken ct = default);

    /// <summary>
    /// Validates a connection string and returns connection details
    /// </summary>
    Task<(bool IsValid, string? EntityName, string? Endpoint, string? ErrorMessage)> ValidateConnectionStringAsync(
        string connectionString, 
        CancellationToken ct = default);
}

