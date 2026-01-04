namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BusLane.Models;

public class ConnectionStringService : IConnectionStringService
{

    public async Task<IEnumerable<QueueInfo>> GetQueuesFromConnectionAsync(string connectionString, CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(connectionString);
        var queues = new List<QueueInfo>();

        await foreach (var queue in adminClient.GetQueuesAsync(ct))
        {
            var properties = await adminClient.GetQueueRuntimePropertiesAsync(queue.Name, ct);
            queues.Add(MapToQueueInfo(queue, properties.Value));
        }

        return queues;
    }

    public async Task<QueueInfo?> GetQueueInfoAsync(string connectionString, string queueName, CancellationToken ct = default)
    {
        try
        {
            var adminClient = new ServiceBusAdministrationClient(connectionString);
            var queue = await adminClient.GetQueueAsync(queueName, ct);
            var properties = await adminClient.GetQueueRuntimePropertiesAsync(queueName, ct);

            return MapToQueueInfo(queue.Value, properties.Value);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<TopicInfo>> GetTopicsFromConnectionAsync(string connectionString, CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(connectionString);
        var topics = new List<TopicInfo>();

        await foreach (var topic in adminClient.GetTopicsAsync(ct))
        {
            var properties = await adminClient.GetTopicRuntimePropertiesAsync(topic.Name, ct);
            topics.Add(MapToTopicInfo(topic, properties.Value));
        }

        return topics;
    }

    public async Task<TopicInfo?> GetTopicInfoAsync(string connectionString, string topicName, CancellationToken ct = default)
    {
        try
        {
            var adminClient = new ServiceBusAdministrationClient(connectionString);
            var topic = await adminClient.GetTopicAsync(topicName, ct);
            var properties = await adminClient.GetTopicRuntimePropertiesAsync(topicName, ct);

            return MapToTopicInfo(topic.Value, properties.Value);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<SubscriptionInfo>> GetTopicSubscriptionsAsync(string connectionString, string topicName, CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(connectionString);
        var subscriptions = new List<SubscriptionInfo>();

        await foreach (var sub in adminClient.GetSubscriptionsAsync(topicName, ct))
        {
            var properties = await adminClient.GetSubscriptionRuntimePropertiesAsync(topicName, sub.SubscriptionName, ct);
            subscriptions.Add(new SubscriptionInfo(
                sub.SubscriptionName,
                topicName,
                properties.Value.TotalMessageCount,
                properties.Value.ActiveMessageCount,
                properties.Value.DeadLetterMessageCount,
                properties.Value.AccessedAt,
                sub.RequiresSession
            ));
        }

        return subscriptions;
    }

    public async Task<NamespaceInfo?> GetNamespaceInfoAsync(string connectionString, CancellationToken ct = default)
    {
        try
        {
            var adminClient = new ServiceBusAdministrationClient(connectionString);
            var queueNames = new List<string>();
            var topicNames = new List<string>();

            await foreach (var queue in adminClient.GetQueuesAsync(ct))
                queueNames.Add(queue.Name);

            await foreach (var topic in adminClient.GetTopicsAsync(ct))
                topicNames.Add(topic.Name);

            var namespaceName = ExtractNamespaceFromConnectionString(connectionString);

            return new NamespaceInfo(
                namespaceName,
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
        string connectionString,
        string entityName,
        string? subscription,
        int count,
        bool deadLetter,
        bool requiresSession = false,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);

        var messages = requiresSession
            ? await ServiceBusOperations.PeekSessionMessagesAsync(client, entityName, subscription, count, deadLetter, ct)
            : await ServiceBusOperations.PeekStandardMessagesAsync(client, entityName, subscription, count, deadLetter, ct);

        return messages.Select(ServiceBusOperations.MapToMessageInfo);
    }

    public async Task SendMessageAsync(
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
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender(entityName);

        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, scheduledEnqueueTime, properties);

        await sender.SendMessageAsync(msg, ct);
    }

    public async Task PurgeMessagesAsync(
        string connectionString,
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await ServiceBusOperations.PurgeMessagesAsync(client, entityName, subscription, deadLetter, ct);
    }

    public async Task<int> DeleteMessagesAsync(
        string connectionString,
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool deadLetter = false,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        return await ServiceBusOperations.DeleteMessagesAsync(client, entityName, subscription, sequenceNumbers, deadLetter, ct);
    }

    public async Task<int> ResendMessagesAsync(
        string connectionString,
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        return await ServiceBusOperations.ResendMessagesAsync(client, entityName, messages, ct);
    }

    public async Task<int> ResubmitDeadLetterMessagesAsync(
        string connectionString,
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        return await ServiceBusOperations.ResubmitDeadLetterMessagesAsync(client, entityName, subscription, messages, ct);
    }

    public async Task<(bool IsValid, string? EntityName, string? Endpoint, string? ErrorMessage)> ValidateConnectionStringAsync(
        string connectionString,
        CancellationToken ct = default)
    {
        try
        {
            var (endpoint, entityPath) = ParseConnectionString(connectionString);

            if (endpoint == null)
                return (false, null, null, "Invalid connection string format: missing endpoint");

            await using var client = new ServiceBusClient(connectionString);

            if (!string.IsNullOrEmpty(entityPath))
                return (true, entityPath, endpoint, null);

            // Namespace-level connection string - validate by listing entities
            var adminClient = new ServiceBusAdministrationClient(connectionString);
            await foreach (var _ in adminClient.GetQueuesAsync(ct))
                break;

            return (true, null, endpoint, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    #region Private Methods

    private static QueueInfo MapToQueueInfo(QueueProperties queue, QueueRuntimeProperties properties) => new(
        queue.Name,
        properties.TotalMessageCount,
        properties.ActiveMessageCount,
        properties.DeadLetterMessageCount,
        properties.ScheduledMessageCount,
        properties.SizeInBytes,
        properties.AccessedAt,
        queue.RequiresSession,
        queue.DefaultMessageTimeToLive,
        queue.LockDuration
    );

    private static TopicInfo MapToTopicInfo(TopicProperties topic, TopicRuntimeProperties properties) => new(
        topic.Name,
        properties.SizeInBytes,
        properties.SubscriptionCount,
        properties.AccessedAt,
        topic.DefaultMessageTimeToLive
    );

    private static string? ExtractNamespaceFromConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';');
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

    private static (string? Endpoint, string? EntityPath) ParseConnectionString(string connectionString)
    {
        string? endpoint = null;
        string? entityPath = null;

        foreach (var part in connectionString.Split(';'))
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

