using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using BusLane.Models;

namespace BusLane.Services;

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
            namespaces.Add(new ServiceBusNamespace(
                ns.Id.ToString(),
                ns.Data.Name,
                ns.Id.ResourceGroupName ?? "",
                subscriptionId,
                ns.Data.Location.DisplayName,
                ns.Data.ServiceBusEndpoint
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
        if (_auth.Credential == null) return [];
        
        await using var client = new ServiceBusClient(endpoint, _auth.Credential);
        
        var entityPath = subscription != null 
            ? $"{queueOrTopic}/subscriptions/{subscription}" 
            : queueOrTopic;
        
        if (deadLetter)
            entityPath = $"{entityPath}/$deadletterqueue";
        
        IReadOnlyList<ServiceBusReceivedMessage> messages;
        
        if (requiresSession)
        {
            // For session-enabled entities, we need to accept sessions to peek messages
            var allMessages = new List<ServiceBusReceivedMessage>();
            var sessionsChecked = new HashSet<string>();
            var maxSessions = 10; // Limit the number of sessions to check
            
            try
            {
                while (allMessages.Count < count && sessionsChecked.Count < maxSessions)
                {
                    try
                    {
                        var sessionReceiver = await client.AcceptNextSessionAsync(entityPath, new ServiceBusSessionReceiverOptions
                        {
                            ReceiveMode = ServiceBusReceiveMode.PeekLock
                        }, ct);
                        
                        if (sessionsChecked.Contains(sessionReceiver.SessionId))
                        {
                            await sessionReceiver.DisposeAsync();
                            break;
                        }
                        
                        sessionsChecked.Add(sessionReceiver.SessionId);
                        
                        var sessionMessages = await sessionReceiver.PeekMessagesAsync(count - allMessages.Count, cancellationToken: ct);
                        allMessages.AddRange(sessionMessages);
                        
                        await sessionReceiver.DisposeAsync();
                    }
                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
                    {
                        // No more sessions available
                        break;
                    }
                }
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                // No sessions available at all
            }
            
            messages = allMessages;
        }
        else
        {
            var receiver = client.CreateReceiver(entityPath);
            messages = await receiver.PeekMessagesAsync(count, cancellationToken: ct);
        }
        
        return messages.Select(m => new MessageInfo(
            m.MessageId,
            m.CorrelationId,
            m.ContentType,
            m.Body.ToString(),
            m.EnqueuedTime,
            m.ScheduledEnqueueTime == default ? null : m.ScheduledEnqueueTime,
            m.SequenceNumber,
            m.DeliveryCount,
            m.SessionId,
            m.ApplicationProperties.ToDictionary(k => k.Key, v => v.Value)
        ));
    }

    public async Task SendMessageAsync(
        string endpoint, string queueOrTopic, string body, 
        IDictionary<string, object>? properties, CancellationToken ct = default)
    {
        if (_auth.Credential == null) return;
        
        await using var client = new ServiceBusClient(endpoint, _auth.Credential);
        var sender = client.CreateSender(queueOrTopic);
        
        var msg = new ServiceBusMessage(body);
        if (properties != null)
        {
            foreach (var (key, value) in properties)
                msg.ApplicationProperties[key] = value;
        }
        
        await sender.SendMessageAsync(msg, ct);
    }

    public async Task DeleteMessageAsync(
        string endpoint, string queueOrTopic, string? subscription, 
        long sequenceNumber, CancellationToken ct = default)
    {
        if (_auth.Credential == null) return;
        
        await using var client = new ServiceBusClient(endpoint, _auth.Credential);
        var entityPath = subscription != null 
            ? $"{queueOrTopic}/subscriptions/{subscription}" 
            : queueOrTopic;
        
        var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        
        var msg = await receiver.ReceiveDeferredMessageAsync(sequenceNumber, ct);
        if (msg != null)
            await receiver.CompleteMessageAsync(msg, ct);
    }

    public async Task PurgeMessagesAsync(
        string endpoint, string queueOrTopic, string? subscription, 
        bool deadLetter, CancellationToken ct = default)
    {
        if (_auth.Credential == null) return;
        
        await using var client = new ServiceBusClient(endpoint, _auth.Credential);
        var entityPath = subscription != null 
            ? $"{queueOrTopic}/subscriptions/{subscription}" 
            : queueOrTopic;
        
        if (deadLetter)
            entityPath = $"{entityPath}/$deadletterqueue";
        
        var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
        
        while (!ct.IsCancellationRequested)
        {
            var msgs = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(5), ct);
            if (msgs.Count == 0) break;
        }
    }
}
