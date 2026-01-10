namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BusLane.Models;

/// <summary>
/// Service Bus operations using a connection string for authentication.
/// </summary>
public class ConnectionStringOperations : IConnectionStringOperations
{
    private readonly string _connectionString;

    public ConnectionStringOperations(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(_connectionString);
        var queues = new List<QueueInfo>();

        await foreach (var queue in adminClient.GetQueuesAsync(ct))
        {
            var props = await adminClient.GetQueueRuntimePropertiesAsync(queue.Name, ct);
            queues.Add(MapToQueueInfo(queue, props.Value));
        }

        return queues;
    }

    public async Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken ct = default)
    {
        try
        {
            var adminClient = new ServiceBusAdministrationClient(_connectionString);
            var queue = await adminClient.GetQueueAsync(queueName, ct);
            var props = await adminClient.GetQueueRuntimePropertiesAsync(queueName, ct);
            return MapToQueueInfo(queue.Value, props.Value);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(_connectionString);
        var topics = new List<TopicInfo>();

        await foreach (var topic in adminClient.GetTopicsAsync(ct))
        {
            var props = await adminClient.GetTopicRuntimePropertiesAsync(topic.Name, ct);
            topics.Add(MapToTopicInfo(topic, props.Value));
        }

        return topics;
    }

    public async Task<TopicInfo?> GetTopicInfoAsync(string topicName, CancellationToken ct = default)
    {
        try
        {
            var adminClient = new ServiceBusAdministrationClient(_connectionString);
            var topic = await adminClient.GetTopicAsync(topicName, ct);
            var props = await adminClient.GetTopicRuntimePropertiesAsync(topicName, ct);
            return MapToTopicInfo(topic.Value, props.Value);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(_connectionString);
        var subscriptions = new List<SubscriptionInfo>();

        await foreach (var sub in adminClient.GetSubscriptionsAsync(topicName, ct))
        {
            var props = await adminClient.GetSubscriptionRuntimePropertiesAsync(topicName, sub.SubscriptionName, ct);
            subscriptions.Add(new SubscriptionInfo(
                sub.SubscriptionName,
                topicName,
                props.Value.TotalMessageCount,
                props.Value.ActiveMessageCount,
                props.Value.DeadLetterMessageCount,
                props.Value.AccessedAt,
                sub.RequiresSession
            ));
        }

        return subscriptions;
    }

    public async Task<NamespaceInfo?> GetNamespaceInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var adminClient = new ServiceBusAdministrationClient(_connectionString);
            var queueNames = new List<string>();
            var topicNames = new List<string>();

            await foreach (var queue in adminClient.GetQueuesAsync(ct))
                queueNames.Add(queue.Name);

            await foreach (var topic in adminClient.GetTopicsAsync(ct))
                topicNames.Add(topic.Name);

            return new NamespaceInfo(
                ExtractNamespaceFromConnectionString(),
                queueNames.Count,
                topicNames.Count,
                queueNames.AsReadOnly(),
                topicNames.AsReadOnly()
            );
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<MessageInfo>> PeekMessagesAsync(
        string entityName, string? subscription, int count, bool deadLetter, 
        bool requiresSession = false, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(_connectionString);

        var messages = requiresSession
            ? await ServiceBusOperations.PeekSessionMessagesAsync(client, entityName, subscription, count, deadLetter, ct)
            : await ServiceBusOperations.PeekStandardMessagesAsync(client, entityName, subscription, count, deadLetter, ct);

        return messages.Select(ServiceBusOperations.MapToMessageInfo);
    }

    public async Task SendMessageAsync(
        string entityName, string body, IDictionary<string, object>? properties,
        string? contentType = null, string? correlationId = null, string? messageId = null,
        string? sessionId = null, string? subject = null, string? to = null,
        string? replyTo = null, string? replyToSessionId = null, string? partitionKey = null,
        TimeSpan? timeToLive = null, DateTimeOffset? scheduledEnqueueTime = null,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(_connectionString);
        await using var sender = client.CreateSender(entityName);

        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, scheduledEnqueueTime, properties);

        await sender.SendMessageAsync(msg, ct);
    }

    public async Task PurgeMessagesAsync(string entityName, string? subscription, bool deadLetter, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(_connectionString);
        await ServiceBusOperations.PurgeMessagesAsync(client, entityName, subscription, deadLetter, ct);
    }

    public async Task<int> DeleteMessagesAsync(
        string entityName, string? subscription, IEnumerable<long> sequenceNumbers, 
        bool deadLetter = false, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(_connectionString);
        return await ServiceBusOperations.DeleteMessagesAsync(client, entityName, subscription, sequenceNumbers, deadLetter, ct);
    }

    public async Task<int> ResendMessagesAsync(string entityName, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(_connectionString);
        return await ServiceBusOperations.ResendMessagesAsync(client, entityName, messages, ct);
    }

    public async Task<int> ResubmitDeadLetterMessagesAsync(
        string entityName, string? subscription, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(_connectionString);
        return await ServiceBusOperations.ResubmitDeadLetterMessagesAsync(client, entityName, subscription, messages, ct);
    }

    public async Task<(bool IsValid, string? EntityName, string? Endpoint, string? ErrorMessage)> ValidateAsync(CancellationToken ct = default)
    {
        try
        {
            var (endpoint, entityPath) = ParseConnectionString();

            if (endpoint == null)
                return (false, null, null, "Invalid connection string format: missing endpoint");

            await using var client = new ServiceBusClient(_connectionString);

            if (!string.IsNullOrEmpty(entityPath))
                return (true, entityPath, endpoint, null);

            // Namespace-level connection string - validate by listing entities
            var adminClient = new ServiceBusAdministrationClient(_connectionString);
            await foreach (var _ in adminClient.GetQueuesAsync(ct))
                break;

            return (true, null, endpoint, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    #region Private Helpers

    private static QueueInfo MapToQueueInfo(QueueProperties queue, QueueRuntimeProperties props) => new(
        queue.Name,
        props.TotalMessageCount,
        props.ActiveMessageCount,
        props.DeadLetterMessageCount,
        props.ScheduledMessageCount,
        props.SizeInBytes,
        props.AccessedAt,
        queue.RequiresSession,
        queue.DefaultMessageTimeToLive,
        queue.LockDuration
    );

    private static TopicInfo MapToTopicInfo(TopicProperties topic, TopicRuntimeProperties props) => new(
        topic.Name,
        props.SizeInBytes,
        props.SubscriptionCount,
        props.AccessedAt,
        topic.DefaultMessageTimeToLive
    );

    private string? ExtractNamespaceFromConnectionString()
    {
        var parts = _connectionString.Split(';');
        foreach (var part in parts)
        {
            if (!part.StartsWith("Endpoint=sb://", StringComparison.OrdinalIgnoreCase))
                continue;

            var endpoint = part["Endpoint=sb://".Length..];
            var dotIndex = endpoint.IndexOf('.');
            return dotIndex > 0 ? endpoint[..dotIndex] : null;
        }
        return null;
    }

    private (string? Endpoint, string? EntityPath) ParseConnectionString()
    {
        string? endpoint = null;
        string? entityPath = null;

        foreach (var part in _connectionString.Split(';'))
        {
            if (part.StartsWith("Endpoint=sb://", StringComparison.OrdinalIgnoreCase))
                endpoint = part["Endpoint=sb://".Length..].TrimEnd('/');
            else if (part.StartsWith("EntityPath=", StringComparison.OrdinalIgnoreCase))
                entityPath = part["EntityPath=".Length..];
        }

        return (endpoint, entityPath);
    }

    #endregion
}
