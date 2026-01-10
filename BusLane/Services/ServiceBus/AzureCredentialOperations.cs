namespace BusLane.Services.ServiceBus;

using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager.ServiceBus;
using BusLane.Models;

/// <summary>
/// Service Bus operations using Azure credential (DefaultAzureCredential/InteractiveBrowserCredential) for authentication.
/// </summary>
public class AzureCredentialOperations : IAzureCredentialOperations
{
    private readonly string _endpoint;
    private readonly string _namespaceId;
    private readonly TokenCredential _credential;
    private readonly Func<ServiceBusNamespaceResource> _getNamespaceResource;

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
    }

    public async Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
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
                q.Data.DefaultMessageTimeToLive ?? TimeSpan.FromDays(14),
                q.Data.LockDuration ?? TimeSpan.FromMinutes(1)
            ));
        }
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
                q.Data.DefaultMessageTimeToLive ?? TimeSpan.FromDays(14),
                q.Data.LockDuration ?? TimeSpan.FromMinutes(1)
            );
        }
        catch
        {
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
                t.Data.DefaultMessageTimeToLive ?? TimeSpan.FromDays(14)
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
                t.Data.DefaultMessageTimeToLive ?? TimeSpan.FromDays(14)
            );
        }
        catch
        {
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
        await using var client = CreateClient();

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
        await using var client = CreateClient();
        await using var sender = client.CreateSender(entityName);

        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, scheduledEnqueueTime, properties);

        await sender.SendMessageAsync(msg, ct);
    }

    public async Task PurgeMessagesAsync(string entityName, string? subscription, bool deadLetter, CancellationToken ct = default)
    {
        await using var client = CreateClient();
        await ServiceBusOperations.PurgeMessagesAsync(client, entityName, subscription, deadLetter, ct);
    }

    public async Task<int> DeleteMessagesAsync(
        string entityName, string? subscription, IEnumerable<long> sequenceNumbers,
        bool deadLetter = false, CancellationToken ct = default)
    {
        await using var client = CreateClient();
        return await ServiceBusOperations.DeleteMessagesAsync(client, entityName, subscription, sequenceNumbers, deadLetter, ct);
    }

    public async Task<int> ResendMessagesAsync(string entityName, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        await using var client = CreateClient();
        return await ServiceBusOperations.ResendMessagesAsync(client, entityName, messages, ct);
    }

    public async Task<int> ResubmitDeadLetterMessagesAsync(
        string entityName, string? subscription, IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        await using var client = CreateClient();
        return await ServiceBusOperations.ResubmitDeadLetterMessagesAsync(client, entityName, subscription, messages, ct);
    }

    #region Private Helpers

    private ServiceBusClient CreateClient() => new(_endpoint, _credential);

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
