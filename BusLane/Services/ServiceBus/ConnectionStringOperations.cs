namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BusLane.Models;
using Serilog;

/// <summary>
/// Service Bus operations using a connection string for authentication.
/// Uses connection pooling to share clients across tabs with the same connection string.
/// </summary>
public class ConnectionStringOperations : IConnectionStringOperations
{
    private readonly string _connectionString;
    private readonly ServiceBusClientPool _clientPool;
    private readonly Lazy<ServiceBusAdministrationClient> _adminClient;
    private ServiceBusClient? _client;
    private bool _disposed;

    public ConnectionStringOperations(string connectionString, ServiceBusClientPool? clientPool = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _clientPool = clientPool ?? new ServiceBusClientPool();
        _adminClient = new Lazy<ServiceBusAdministrationClient>(() => 
            _clientPool.GetAdminClient(_connectionString));
    }

    private ServiceBusClient Client => _client ??= _clientPool.GetClient(_connectionString);
    private ServiceBusAdministrationClient AdminClient => _adminClient.Value;

    public async Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        // First, collect all queue properties
        var queueProperties = new List<QueueProperties>();
        await foreach (var queue in AdminClient.GetQueuesAsync(ct))
        {
            queueProperties.Add(queue);
        }

        if (queueProperties.Count == 0)
            return [];

        // Fetch runtime properties in parallel for better performance
        var runtimeTasks = queueProperties.Select(q =>
            AdminClient.GetQueueRuntimePropertiesAsync(q.Name, ct));
        var runtimeResults = await Task.WhenAll(runtimeTasks);

        // Combine properties and runtime info
        return queueProperties.Zip(runtimeResults, (queue, runtime) =>
            MapToQueueInfo(queue, runtime.Value)).ToList();
    }

    public async Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken ct = default)
    {
        try
        {
            var queue = await AdminClient.GetQueueAsync(queueName, ct);
            var props = await AdminClient.GetQueueRuntimePropertiesAsync(queueName, ct);
            return MapToQueueInfo(queue.Value, props.Value);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get queue info for {QueueName}", queueName);
            return null;
        }
    }

    public async Task<IEnumerable<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        // First, collect all topic properties
        var topicProperties = new List<TopicProperties>();
        await foreach (var topic in AdminClient.GetTopicsAsync(ct))
        {
            topicProperties.Add(topic);
        }

        if (topicProperties.Count == 0)
            return [];

        // Fetch runtime properties in parallel for better performance
        var runtimeTasks = topicProperties.Select(t =>
            AdminClient.GetTopicRuntimePropertiesAsync(t.Name, ct));
        var runtimeResults = await Task.WhenAll(runtimeTasks);

        // Combine properties and runtime info
        return topicProperties.Zip(runtimeResults, (topic, runtime) =>
            MapToTopicInfo(topic, runtime.Value)).ToList();
    }

    public async Task<TopicInfo?> GetTopicInfoAsync(string topicName, CancellationToken ct = default)
    {
        try
        {
            var topic = await AdminClient.GetTopicAsync(topicName, ct);
            var props = await AdminClient.GetTopicRuntimePropertiesAsync(topicName, ct);
            return MapToTopicInfo(topic.Value, props.Value);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get topic info for {TopicName}", topicName);
            return null;
        }
    }

    public async Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        // First, collect all subscription properties
        var subscriptionProperties = new List<SubscriptionProperties>();
        await foreach (var sub in AdminClient.GetSubscriptionsAsync(topicName, ct))
        {
            subscriptionProperties.Add(sub);
        }

        if (subscriptionProperties.Count == 0)
            return [];

        // Fetch runtime properties in parallel for better performance
        var runtimeTasks = subscriptionProperties.Select(s =>
            AdminClient.GetSubscriptionRuntimePropertiesAsync(topicName, s.SubscriptionName, ct));
        var runtimeResults = await Task.WhenAll(runtimeTasks);

        // Combine properties and runtime info
        return subscriptionProperties.Zip(runtimeResults, (sub, runtime) => new SubscriptionInfo(
            sub.SubscriptionName,
            topicName,
            runtime.Value.TotalMessageCount,
            runtime.Value.ActiveMessageCount,
            runtime.Value.DeadLetterMessageCount,
            runtime.Value.AccessedAt,
            sub.RequiresSession
        )).ToList();
    }

    public async Task<NamespaceInfo?> GetNamespaceInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var queueNames = new List<string>();
            var topicNames = new List<string>();

            await foreach (var queue in AdminClient.GetQueuesAsync(ct))
                queueNames.Add(queue.Name);

            await foreach (var topic in AdminClient.GetTopicsAsync(ct))
                topicNames.Add(topic.Name);

            return new NamespaceInfo(
                ExtractNamespaceFromConnectionString(),
                queueNames.Count,
                topicNames.Count,
                queueNames.AsReadOnly(),
                topicNames.AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get namespace info");
            return null;
        }
    }

    public async Task<IEnumerable<MessageInfo>> PeekMessagesAsync(
        string entityName, string? subscription, int count, bool deadLetter,
        bool requiresSession = false, CancellationToken ct = default)
    {
        var messages = requiresSession
            ? await ServiceBusOperations.PeekSessionMessagesAsync(Client, entityName, subscription, count, deadLetter, ct)
            : await ServiceBusOperations.PeekStandardMessagesAsync(Client, entityName, subscription, count, deadLetter, ct);

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
        await using var sender = Client.CreateSender(entityName);

        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, scheduledEnqueueTime, properties);

        await sender.SendMessageAsync(msg, ct);
    }

    public async Task PurgeMessagesAsync(string entityName, string? subscription, bool deadLetter, CancellationToken ct = default)
    {
        await ServiceBusOperations.PurgeMessagesAsync(Client, entityName, subscription, deadLetter, ct);
    }

    public async Task<int> DeleteMessagesAsync(
        string entityName, string? subscription, IEnumerable<long> sequenceNumbers,
        bool deadLetter = false, CancellationToken ct = default)
    {
        return await ServiceBusOperations.DeleteMessagesAsync(Client, entityName, subscription, sequenceNumbers, deadLetter, ct);
    }

    public async Task<int> ResendMessagesAsync(string entityName, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        Log.Debug("Resending {Count} messages to {EntityName}", messageList.Count, entityName);
        var resent = await ServiceBusOperations.ResendMessagesAsync(Client, entityName, messageList, ct);
        Log.Information("Resent {ResentCount} messages to {EntityName}", resent, entityName);
        return resent;
    }

    public async Task<int> ResubmitDeadLetterMessagesAsync(
        string entityName, string? subscription, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        Log.Debug("Resubmitting {Count} dead letter messages from {EntityName}", messageList.Count, entityName);
        var resubmitted = await ServiceBusOperations.ResubmitDeadLetterMessagesAsync(Client, entityName, subscription, messageList, ct);
        Log.Information("Resubmitted {ResubmittedCount} dead letter messages from {EntityName}", resubmitted, entityName);
        return resubmitted;
    }

    public async Task<(bool IsValid, string? EntityName, string? Endpoint, string? ErrorMessage)> ValidateAsync(CancellationToken ct = default)
    {
        try
        {
            var (endpoint, entityPath) = ParseConnectionString();

            if (endpoint == null)
                return (false, null, null, "Invalid connection string format: missing endpoint");

            if (!string.IsNullOrEmpty(entityPath))
                return (true, entityPath, endpoint, null);

            // Namespace-level connection string - validate by listing entities
            await foreach (var _ in AdminClient.GetQueuesAsync(ct))
                break;

            return (true, null, endpoint, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Return the client to the pool instead of disposing directly
        if (_client != null)
        {
            _clientPool.ReturnClient(_connectionString, _client);
        }
        
        // ServiceBusAdministrationClient doesn't implement IAsyncDisposable
        // It's lightweight and will be garbage collected
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
