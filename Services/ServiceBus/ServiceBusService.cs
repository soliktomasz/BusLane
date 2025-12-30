namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.ResourceManager.ServiceBus;
using BusLane.Models;
using BusLane.Services.Auth;

public class ServiceBusService : IServiceBusService
{
    private readonly IAzureAuthService _auth;
    private const int MaxSessionsToCheck = 10;
    private const int PurgeBatchSize = 100;
    private static readonly TimeSpan PurgeReceiveTimeout = TimeSpan.FromSeconds(5);

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
            ? await PeekSessionMessagesAsync(client, queueOrTopic, subscription, count, deadLetter, ct)
            : await PeekStandardMessagesAsync(client, queueOrTopic, subscription, count, deadLetter, ct);

        return messages.Select(MapToMessageInfo);
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
        ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
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

        var options = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
            SubQueue = deadLetter ? SubQueue.DeadLetter : SubQueue.None
        };

        await using var receiver = subscription != null
            ? client.CreateReceiver(queueOrTopic, subscription, options)
            : client.CreateReceiver(queueOrTopic, options);

        while (!ct.IsCancellationRequested)
        {
            var msgs = await receiver.ReceiveMessagesAsync(PurgeBatchSize, PurgeReceiveTimeout, ct);
            if (msgs.Count == 0) break;
        }
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

    private static MessageInfo MapToMessageInfo(ServiceBusReceivedMessage m) => new(
        m.MessageId,
        m.CorrelationId,
        m.ContentType,
        m.Body.ToString(),
        m.EnqueuedTime,
        m.ScheduledEnqueueTime == default ? null : m.ScheduledEnqueueTime,
        m.SequenceNumber,
        m.DeliveryCount,
        m.SessionId,
        m.ApplicationProperties.ToDictionary(k => k.Key, v => v.Value),
        m.Subject,
        m.To,
        m.ReplyTo,
        m.ReplyToSessionId,
        m.PartitionKey,
        m.TimeToLive,
        m.ExpiresAt,
        m.LockToken,
        m.LockedUntil,
        m.DeadLetterSource,
        m.DeadLetterReason,
        m.DeadLetterErrorDescription
    );

    private static void ApplyMessageProperties(
        ServiceBusMessage msg,
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
        IDictionary<string, object>? properties)
    {
        if (!string.IsNullOrWhiteSpace(contentType)) msg.ContentType = contentType;
        if (!string.IsNullOrWhiteSpace(correlationId)) msg.CorrelationId = correlationId;
        if (!string.IsNullOrWhiteSpace(messageId)) msg.MessageId = messageId;
        if (!string.IsNullOrWhiteSpace(sessionId)) msg.SessionId = sessionId;
        if (!string.IsNullOrWhiteSpace(subject)) msg.Subject = subject;
        if (!string.IsNullOrWhiteSpace(to)) msg.To = to;
        if (!string.IsNullOrWhiteSpace(replyTo)) msg.ReplyTo = replyTo;
        if (!string.IsNullOrWhiteSpace(replyToSessionId)) msg.ReplyToSessionId = replyToSessionId;
        if (!string.IsNullOrWhiteSpace(partitionKey)) msg.PartitionKey = partitionKey;
        if (timeToLive.HasValue) msg.TimeToLive = timeToLive.Value;
        if (scheduledEnqueueTime.HasValue) msg.ScheduledEnqueueTime = scheduledEnqueueTime.Value;

        if (properties == null) return;
        foreach (var (key, value) in properties)
            msg.ApplicationProperties[key] = value;
    }

    private static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekStandardMessagesAsync(
        ServiceBusClient client, string queueOrTopic, string? subscription,
        int count, bool deadLetter, CancellationToken ct)
    {
        await using var receiver = CreateReceiver(client, queueOrTopic, subscription, deadLetter);
        return await receiver.PeekMessagesAsync(count, cancellationToken: ct);
    }

    private static ServiceBusReceiver CreateReceiver(
        ServiceBusClient client, string queueOrTopic, string? subscription, bool deadLetter)
    {
        var options = deadLetter ? new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter } : null;

        return subscription != null
            ? (options != null ? client.CreateReceiver(queueOrTopic, subscription, options) : client.CreateReceiver(queueOrTopic, subscription))
            : (options != null ? client.CreateReceiver(queueOrTopic, options) : client.CreateReceiver(queueOrTopic));
    }

    private static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekSessionMessagesAsync(
        ServiceBusClient client, string queueOrTopic, string? subscription,
        int count, bool deadLetter, CancellationToken ct)
    {
        var allMessages = new List<ServiceBusReceivedMessage>();
        var sessionsChecked = new HashSet<string>();

        try
        {
            while (allMessages.Count < count && sessionsChecked.Count < MaxSessionsToCheck)
            {
                try
                {
                    var sessionReceiver = await AcceptNextSessionReceiverAsync(
                        client, queueOrTopic, subscription, deadLetter, ct);

                    if (sessionsChecked.Contains(sessionReceiver.SessionId))
                    {
                        await sessionReceiver.DisposeAsync();
                        break;
                    }

                    sessionsChecked.Add(sessionReceiver.SessionId);

                    var remaining = count - allMessages.Count;
                    var sessionMessages = await sessionReceiver.PeekMessagesAsync(remaining, cancellationToken: ct);
                    allMessages.AddRange(sessionMessages);

                    await sessionReceiver.DisposeAsync();
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
                {
                    break; // No more sessions available
                }
            }
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            // No sessions available at all - return empty list
        }

        return allMessages;
    }

    private static async Task<ServiceBusSessionReceiver> AcceptNextSessionReceiverAsync(
        ServiceBusClient client, string queueOrTopic, string? subscription,
        bool deadLetter, CancellationToken ct)
    {
        var sessionOptions = new ServiceBusSessionReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock };

        if (subscription != null)
        {
            return deadLetter
                ? await client.AcceptNextSessionAsync($"{queueOrTopic}/Subscriptions/{subscription}/$DeadLetterQueue", sessionOptions, ct)
                : await client.AcceptNextSessionAsync(queueOrTopic, subscription, sessionOptions, ct);
        }

        return deadLetter
            ? await client.AcceptNextSessionAsync($"{queueOrTopic}/$DeadLetterQueue", sessionOptions, ct)
            : await client.AcceptNextSessionAsync(queueOrTopic, sessionOptions, ct);
    }

    #endregion
}
