namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.ResourceManager.ServiceBus;
using BusLane.Models;
using BusLane.Services.Auth;

public class ServiceBusService : IServiceBusService
{
    private readonly IAzureAuthService _auth;

    public ServiceBusService(IAzureAuthService auth) => _auth = auth;

    public async Task<IEnumerable<AzureSubscription>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        if (_auth.ArmClient == null) return [];
        
        var subs = new List<AzureSubscription>();
        await foreach (var sub in _auth.ArmClient.GetSubscriptions().GetAllAsync(ct))
        {
            subs.Add(new AzureSubscription(sub.Data.SubscriptionId, sub.Data.DisplayName));
        }
        return subs;
    }

    public async Task<IEnumerable<ServiceBusNamespace>> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default)
    {
        if (_auth.ArmClient == null) return [];
        
        var sub = _auth.ArmClient.GetSubscriptionResource(
            new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}")
        );
        
        var namespaces = new List<ServiceBusNamespace>();
        await foreach (var ns in sub.GetServiceBusNamespacesAsync(ct))
        {
            // Construct endpoint from namespace name - always derive from namespace name for reliability
            // ns.Data.ServiceBusEndpoint can be null, empty, or return a URI format that needs parsing
            var endpoint = $"{ns.Data.Name}.servicebus.windows.net";
            
            namespaces.Add(new ServiceBusNamespace(
                ns.Id.ToString(),
                ns.Data.Name,
                ns.Id.ResourceGroupName ?? "",
                subscriptionId,
                ns.Data.Location.DisplayName ?? "",
                endpoint
            ));
        }
        return namespaces;
    }

    public async Task<IEnumerable<QueueInfo>> GetQueuesAsync(string namespaceId, CancellationToken ct = default)
    {
        if (_auth.ArmClient == null) return [];
        
        var ns = _auth.ArmClient.GetServiceBusNamespaceResource(new Azure.Core.ResourceIdentifier(namespaceId));
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

    public async Task<IEnumerable<TopicInfo>> GetTopicsAsync(string namespaceId, CancellationToken ct = default)
    {
        if (_auth.ArmClient == null) return [];
        
        var ns = _auth.ArmClient.GetServiceBusNamespaceResource(new Azure.Core.ResourceIdentifier(namespaceId));
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

    public async Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string namespaceId, string topicName, CancellationToken ct = default)
    {
        if (_auth.ArmClient == null) return [];
        
        var ns = _auth.ArmClient.GetServiceBusNamespaceResource(new Azure.Core.ResourceIdentifier(namespaceId));
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
        string endpoint, string queueOrTopic, string? subscription,
        int count, bool deadLetter, bool requiresSession = false, CancellationToken ct = default)
    {
        EnsureAuthenticated();
        queueOrTopic = NormalizeEntityPath(queueOrTopic);

        await using var client = CreateClient(endpoint);

        var messages = requiresSession
            ? await ServiceBusOperations.PeekSessionMessagesAsync(client, queueOrTopic, subscription, count, deadLetter, ct)
            : await ServiceBusOperations.PeekStandardMessagesAsync(client, queueOrTopic, subscription, count, deadLetter, ct);

        return messages.Select(ServiceBusOperations.MapToMessageInfo);
    }

    public Task SendMessageAsync(
        string endpoint, string queueOrTopic, string body,
        IDictionary<string, object>? properties, CancellationToken ct = default)
    {
        return SendMessageAsync(endpoint, queueOrTopic, body, properties,
            null, null, null, null, null, null, null, null, null, null, null, ct);
    }

    public async Task SendMessageAsync(
        string endpoint,
        string queueOrTopic,
        string body,
        IDictionary<string, object>? properties,
        string? contentType,
        string? correlationId,
        string? messageId,
        string? sessionId,
        string? subject,
        string? to,
        string? replyTo,
        string? replyToSessionId,
        string? partitionKey,
        TimeSpan? timeToLive,
        DateTimeOffset? scheduledEnqueueTime,
        CancellationToken ct = default)
    {
        EnsureAuthenticated();

        await using var client = CreateClient(endpoint);
        await using var sender = client.CreateSender(queueOrTopic);

        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, scheduledEnqueueTime, properties);

        await sender.SendMessageAsync(msg, ct);
    }

    public async Task DeleteMessageAsync(
        string endpoint, string queueOrTopic, string? subscription,
        long sequenceNumber, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        await using var client = CreateClient(endpoint);

        var receiverOptions = new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock };
        await using var receiver = subscription != null
            ? client.CreateReceiver(queueOrTopic, subscription, receiverOptions)
            : client.CreateReceiver(queueOrTopic, receiverOptions);

        var msg = await receiver.ReceiveDeferredMessageAsync(sequenceNumber, ct);
        if (msg != null)
            await receiver.CompleteMessageAsync(msg, ct);
    }

    public async Task PurgeMessagesAsync(
        string endpoint, string queueOrTopic, string? subscription,
        bool deadLetter, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        await using var client = CreateClient(endpoint);
        await ServiceBusOperations.PurgeMessagesAsync(client, queueOrTopic, subscription, deadLetter, ct);
    }

    public async Task<int> DeleteMessagesAsync(
        string endpoint, string queueOrTopic, string? subscription,
        IEnumerable<long> sequenceNumbers, bool deadLetter = false, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        await using var client = CreateClient(endpoint);
        return await ServiceBusOperations.DeleteMessagesAsync(client, queueOrTopic, subscription, sequenceNumbers, deadLetter, ct);
    }

    public async Task<int> ResendMessagesAsync(
        string endpoint, string queueOrTopic,
        IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        await using var client = CreateClient(endpoint);
        return await ServiceBusOperations.ResendMessagesAsync(client, queueOrTopic, messages, ct);
    }

    public async Task<int> ResubmitDeadLetterMessagesAsync(
        string endpoint, string queueOrTopic, string? subscription,
        IEnumerable<MessageInfo> messages, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        await using var client = CreateClient(endpoint);
        return await ServiceBusOperations.ResubmitDeadLetterMessagesAsync(client, queueOrTopic, subscription, messages, ct);
    }

    #region Private Methods

    private void EnsureAuthenticated()
    {
        if (_auth.Credential == null)
            throw new InvalidOperationException("Not authenticated. Please sign in to Azure first.");
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        var fullyQualifiedNamespace = endpoint
            .Replace("sb://", "")
            .Replace("https://", "")
            .TrimEnd('/');

        if (!fullyQualifiedNamespace.Contains('.'))
            fullyQualifiedNamespace = $"{fullyQualifiedNamespace}.servicebus.windows.net";

        return fullyQualifiedNamespace;
    }

    private static string NormalizeEntityPath(string queueOrTopic) =>
        queueOrTopic.Contains('~') ? queueOrTopic.Replace('~', '/') : queueOrTopic;

    private ServiceBusClient CreateClient(string endpoint) =>
        new(NormalizeEndpoint(endpoint), _auth.Credential);


    #endregion
}
