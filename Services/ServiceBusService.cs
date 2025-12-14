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
        if (_auth.Credential == null) return [];
        
        // Ensure endpoint is in the correct format (fully qualified namespace)
        var fullyQualifiedNamespace = endpoint
            .Replace("sb://", "")
            .Replace("https://", "")
            .TrimEnd('/');
        
        await using var client = new ServiceBusClient(fullyQualifiedNamespace, _auth.Credential);
        
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
                        ServiceBusSessionReceiver sessionReceiver;
                        var sessionOptions = new ServiceBusSessionReceiverOptions
                        {
                            ReceiveMode = ServiceBusReceiveMode.PeekLock
                        };
                        
                        // Use the proper overload for subscriptions vs queues
                        if (subscription != null)
                        {
                            // For topic subscriptions, use the dedicated overload
                            if (deadLetter)
                            {
                                // For dead letter on subscription, we need to use the path format
                                var dlqPath = $"{queueOrTopic}/Subscriptions/{subscription}/$DeadLetterQueue";
                                sessionReceiver = await client.AcceptNextSessionAsync(dlqPath, sessionOptions, ct);
                            }
                            else
                            {
                                // Use the proper SDK overload for subscriptions
                                sessionReceiver = await client.AcceptNextSessionAsync(queueOrTopic, subscription, sessionOptions, ct);
                            }
                        }
                        else
                        {
                            // For queues
                            if (deadLetter)
                            {
                                var dlqPath = $"{queueOrTopic}/$DeadLetterQueue";
                                sessionReceiver = await client.AcceptNextSessionAsync(dlqPath, sessionOptions, ct);
                            }
                            else
                            {
                                sessionReceiver = await client.AcceptNextSessionAsync(queueOrTopic, sessionOptions, ct);
                            }
                        }
                        
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
            // Use the proper overload for subscriptions vs queues
            ServiceBusReceiver receiver;
            if (subscription != null)
            {
                // Use the dedicated subscription overload
                var options = deadLetter ? new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter } : null;
                receiver = options != null 
                    ? client.CreateReceiver(queueOrTopic, subscription, options)
                    : client.CreateReceiver(queueOrTopic, subscription);
            }
            else
            {
                // Queue path
                var options = deadLetter ? new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter } : null;
                receiver = options != null
                    ? client.CreateReceiver(queueOrTopic, options)
                    : client.CreateReceiver(queueOrTopic);
            }
            
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
        await SendMessageAsync(endpoint, queueOrTopic, body, properties, 
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
        if (_auth.Credential == null) return;
        
        // Ensure endpoint is in the correct format (fully qualified namespace)
        var fullyQualifiedNamespace = endpoint
            .Replace("sb://", "")
            .Replace("https://", "")
            .TrimEnd('/');
        
        await using var client = new ServiceBusClient(fullyQualifiedNamespace, _auth.Credential);
        var sender = client.CreateSender(queueOrTopic);
        
        var msg = new ServiceBusMessage(body);
        
        if (!string.IsNullOrWhiteSpace(contentType))
            msg.ContentType = contentType;
        if (!string.IsNullOrWhiteSpace(correlationId))
            msg.CorrelationId = correlationId;
        if (!string.IsNullOrWhiteSpace(messageId))
            msg.MessageId = messageId;
        if (!string.IsNullOrWhiteSpace(sessionId))
            msg.SessionId = sessionId;
        if (!string.IsNullOrWhiteSpace(subject))
            msg.Subject = subject;
        if (!string.IsNullOrWhiteSpace(to))
            msg.To = to;
        if (!string.IsNullOrWhiteSpace(replyTo))
            msg.ReplyTo = replyTo;
        if (!string.IsNullOrWhiteSpace(replyToSessionId))
            msg.ReplyToSessionId = replyToSessionId;
        if (!string.IsNullOrWhiteSpace(partitionKey))
            msg.PartitionKey = partitionKey;
        if (timeToLive.HasValue)
            msg.TimeToLive = timeToLive.Value;
        if (scheduledEnqueueTime.HasValue)
            msg.ScheduledEnqueueTime = scheduledEnqueueTime.Value;
        
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
        
        // Ensure endpoint is in the correct format (fully qualified namespace)
        var fullyQualifiedNamespace = endpoint
            .Replace("sb://", "")
            .Replace("https://", "")
            .TrimEnd('/');
        
        await using var client = new ServiceBusClient(fullyQualifiedNamespace, _auth.Credential);
        
        ServiceBusReceiver receiver;
        if (subscription != null)
        {
            receiver = client.CreateReceiver(queueOrTopic, subscription, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        }
        else
        {
            receiver = client.CreateReceiver(queueOrTopic, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        }
        
        var msg = await receiver.ReceiveDeferredMessageAsync(sequenceNumber, ct);
        if (msg != null)
            await receiver.CompleteMessageAsync(msg, ct);
    }

    public async Task PurgeMessagesAsync(
        string endpoint, string queueOrTopic, string? subscription, 
        bool deadLetter, CancellationToken ct = default)
    {
        if (_auth.Credential == null) return;
        
        // Ensure endpoint is in the correct format (fully qualified namespace)
        var fullyQualifiedNamespace = endpoint
            .Replace("sb://", "")
            .Replace("https://", "")
            .TrimEnd('/');
        
        await using var client = new ServiceBusClient(fullyQualifiedNamespace, _auth.Credential);
        
        ServiceBusReceiver receiver;
        var options = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        };
        
        if (deadLetter)
            options.SubQueue = SubQueue.DeadLetter;
        
        if (subscription != null)
        {
            receiver = client.CreateReceiver(queueOrTopic, subscription, options);
        }
        else
        {
            receiver = client.CreateReceiver(queueOrTopic, options);
        }
        
        while (!ct.IsCancellationRequested)
        {
            var msgs = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(5), ct);
            if (msgs.Count == 0) break;
        }
    }
}
