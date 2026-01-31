namespace BusLane.Services.ServiceBus;

using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager.ServiceBus;
using BusLane.Models;
using Serilog;

/// <summary>
/// Service Bus operations using Azure credential (DefaultAzureCredential/InteractiveBrowserCredential) for authentication.
/// Caches ServiceBusClient for connection pooling.
/// </summary>
public class AzureCredentialOperations : IAzureCredentialOperations
{
    private readonly string _endpoint;
    private readonly string _namespaceId;
    private readonly TokenCredential _credential;
    private readonly Func<ServiceBusNamespaceResource> _getNamespaceResource;
    private readonly Lazy<ServiceBusClient> _client;
    private bool _disposed;

    public string NamespaceId => _namespaceId;

    public AzureCredentialOperations(
        string endpoint,
        string namespaceId,
        TokenCredential credential,
        Func<ServiceBusNamespaceResource> getNamespaceResource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(getNamespaceResource);

        _endpoint = NormalizeEndpoint(endpoint);
        _namespaceId = namespaceId;
        _credential = credential;
        _getNamespaceResource = getNamespaceResource;
        _client = new Lazy<ServiceBusClient>(() => new ServiceBusClient(_endpoint, _credential));
        Log.Debug("AzureCredentialOperations initialized for endpoint {Endpoint}", _endpoint);
    }

    public ServiceBusClient GetClient() => _client.Value;

    public async Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        Log.Debug("Fetching queues from {Endpoint}", _endpoint);
        var ns = _getNamespaceResource();
        var queues = new List<QueueInfo>();

        await foreach (var q in ns.GetServiceBusQueues().GetAllAsync(cancellationToken: ct))
        {
            queues.Add(new QueueInfo(
                q.Data.Name,
                q.Data.MessageCount ?? 0,
                q.Data.CountDetails?.ActiveMessageCount ?? 0,
                q.Data.CountDetails?.DeadLetterMessageCount ?? 0,
                q.Data.CountDetails?.ScheduledMessageCount ?? 0,
                q.Data.SizeInBytes ?? 0,
                q.Data.AccessedOn,
                q.Data.RequiresSession ?? false,
                q.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive,
                q.Data.LockDuration ?? ServiceBusOperations.Options.DefaultLockDuration
            ));
        }
        Log.Debug("Retrieved {Count} queues from {Endpoint}", queues.Count, _endpoint);
        return queues;
    }

    public async Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken ct = default)
    {
        try
        {
            var ns = _getNamespaceResource();
            var queue = await ns.GetServiceBusQueueAsync(queueName, ct);
            var q = queue.Value;

            return new QueueInfo(
                q.Data.Name,
                q.Data.MessageCount ?? 0,
                q.Data.CountDetails?.ActiveMessageCount ?? 0,
                q.Data.CountDetails?.DeadLetterMessageCount ?? 0,
                q.Data.CountDetails?.ScheduledMessageCount ?? 0,
                q.Data.SizeInBytes ?? 0,
                q.Data.AccessedOn,
                q.Data.RequiresSession ?? false,
                q.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive,
                q.Data.LockDuration ?? ServiceBusOperations.Options.DefaultLockDuration
            );
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get queue info for {QueueName}", queueName);
            return null;
        }
    }

    public async Task<IEnumerable<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        var ns = _getNamespaceResource();
        var topics = new List<TopicInfo>();

        await foreach (var t in ns.GetServiceBusTopics().GetAllAsync(cancellationToken: ct))
        {
            topics.Add(new TopicInfo(
                t.Data.Name,
                t.Data.SizeInBytes ?? 0,
                t.Data.SubscriptionCount ?? 0,
                t.Data.AccessedOn,
                t.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive
            ));
        }
        return topics;
    }

    public async Task<TopicInfo?> GetTopicInfoAsync(string topicName, CancellationToken ct = default)
    {
        try
        {
            var ns = _getNamespaceResource();
            var topic = await ns.GetServiceBusTopicAsync(topicName, ct);
            var t = topic.Value;

            return new TopicInfo(
                t.Data.Name,
                t.Data.SizeInBytes ?? 0,
                t.Data.SubscriptionCount ?? 0,
                t.Data.AccessedOn,
                t.Data.DefaultMessageTimeToLive ?? ServiceBusOperations.Options.DefaultMessageTimeToLive
            );
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get topic info for {TopicName}", topicName);
            return null;
        }
    }

    public async Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        var ns = _getNamespaceResource();
        var topic = await ns.GetServiceBusTopicAsync(topicName, ct);
        var subs = new List<SubscriptionInfo>();

        await foreach (var s in topic.Value.GetServiceBusSubscriptions().GetAllAsync(cancellationToken: ct))
        {
            subs.Add(new SubscriptionInfo(
                s.Data.Name,
                topicName,
                s.Data.MessageCount ?? 0,
                s.Data.CountDetails?.ActiveMessageCount ?? 0,
                s.Data.CountDetails?.DeadLetterMessageCount ?? 0,
                s.Data.AccessedOn,
                s.Data.RequiresSession ?? false
            ));
        }
        return subs;
    }

    public async Task<IEnumerable<MessageInfo>> PeekMessagesAsync(
        string entityName, string? subscription, int count, bool deadLetter,
        bool requiresSession = false, CancellationToken ct = default)
    {
        entityName = NormalizeEntityPath(entityName);

        var messages = requiresSession
            ? await ServiceBusOperations.PeekSessionMessagesAsync(GetClient(), entityName, subscription, count, deadLetter, ct)
            : await ServiceBusOperations.PeekStandardMessagesAsync(GetClient(), entityName, subscription, count, deadLetter, ct);

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
        Log.Debug("Sending message to {EntityName} via Azure credential", entityName);
        await using var sender = GetClient().CreateSender(entityName);

        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, scheduledEnqueueTime, properties);

        await sender.SendMessageAsync(msg, ct);
        Log.Information("Message sent to {EntityName} (MessageId: {MessageId})", entityName, msg.MessageId);
    }

    public async Task PurgeMessagesAsync(string entityName, string? subscription, bool deadLetter, CancellationToken ct = default)
    {
        Log.Information("Purging messages from {EntityName}{Subscription} (DeadLetter: {DeadLetter})",
            entityName, subscription != null ? $"/{subscription}" : "", deadLetter);
        await ServiceBusOperations.PurgeMessagesAsync(GetClient(), entityName, subscription, deadLetter, ct);
        Log.Information("Purge completed for {EntityName}", entityName);
    }

    public async Task<int> DeleteMessagesAsync(
        string entityName, string? subscription, IEnumerable<long> sequenceNumbers,
        bool deadLetter = false, CancellationToken ct = default)
    {
        var sequenceList = sequenceNumbers.ToList();
        Log.Debug("Deleting {Count} messages from {EntityName}", sequenceList.Count, entityName);
        var deleted = await ServiceBusOperations.DeleteMessagesAsync(GetClient(), entityName, subscription, sequenceList, deadLetter, ct);
        Log.Information("Deleted {DeletedCount} messages from {EntityName}", deleted, entityName);
        return deleted;
    }

    public async Task<int> ResendMessagesAsync(string entityName, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        Log.Debug("Resending {Count} messages to {EntityName}", messageList.Count, entityName);
        var resent = await ServiceBusOperations.ResendMessagesAsync(GetClient(), entityName, messageList, ct);
        Log.Information("Resent {ResentCount} messages to {EntityName}", resent, entityName);
        return resent;
    }

    public async Task<int> ResubmitDeadLetterMessagesAsync(
        string entityName, string? subscription, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        Log.Debug("Resubmitting {Count} dead letter messages from {EntityName}", messageList.Count, entityName);
        var resubmitted = await ServiceBusOperations.ResubmitDeadLetterMessagesAsync(GetClient(), entityName, subscription, messageList, ct);
        Log.Information("Resubmitted {ResubmittedCount} dead letter messages from {EntityName}", resubmitted, entityName);
        return resubmitted;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_client.IsValueCreated)
        {
            await _client.Value.DisposeAsync();
        }
    }

    #region Private Helpers

    private static string NormalizeEndpoint(string endpoint)
    {
        var fqn = endpoint
            .Replace("sb://", "")
            .Replace("https://", "")
            .TrimEnd('/');

        if (!fqn.Contains('.'))
            fqn = $"{fqn}.servicebus.windows.net";

        return fqn;
    }

    private static string NormalizeEntityPath(string entityPath) =>
        entityPath.Contains('~') ? entityPath.Replace('~', '/') : entityPath;

    #endregion
}
