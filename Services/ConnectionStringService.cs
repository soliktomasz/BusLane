using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BusLane.Models;

namespace BusLane.Services;

public class ConnectionStringService : IConnectionStringService
{
    public async Task<IEnumerable<QueueInfo>> GetQueuesFromConnectionAsync(string connectionString, CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(connectionString);
        var queues = new List<QueueInfo>();

        await foreach (var queue in adminClient.GetQueuesAsync(ct))
        {
            var properties = await adminClient.GetQueueRuntimePropertiesAsync(queue.Name, ct);
            queues.Add(new QueueInfo(
                queue.Name,
                properties.Value.TotalMessageCount,
                properties.Value.ActiveMessageCount,
                properties.Value.DeadLetterMessageCount,
                properties.Value.ScheduledMessageCount,
                properties.Value.SizeInBytes,
                properties.Value.AccessedAt,
                queue.RequiresSession,
                queue.DefaultMessageTimeToLive,
                queue.LockDuration
            ));
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

            return new QueueInfo(
                queue.Value.Name,
                properties.Value.TotalMessageCount,
                properties.Value.ActiveMessageCount,
                properties.Value.DeadLetterMessageCount,
                properties.Value.ScheduledMessageCount,
                properties.Value.SizeInBytes,
                properties.Value.AccessedAt,
                queue.Value.RequiresSession,
                queue.Value.DefaultMessageTimeToLive,
                queue.Value.LockDuration
            );
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
            topics.Add(new TopicInfo(
                topic.Name,
                properties.Value.SizeInBytes,
                properties.Value.SubscriptionCount,
                properties.Value.AccessedAt,
                topic.DefaultMessageTimeToLive
            ));
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

            return new TopicInfo(
                topic.Value.Name,
                properties.Value.SizeInBytes,
                properties.Value.SubscriptionCount,
                properties.Value.AccessedAt,
                topic.Value.DefaultMessageTimeToLive
            );
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

            // Get all queues
            await foreach (var queue in adminClient.GetQueuesAsync(ct))
            {
                queueNames.Add(queue.Name);
            }

            // Get all topics
            await foreach (var topic in adminClient.GetTopicsAsync(ct))
            {
                topicNames.Add(topic.Name);
            }

            // Extract namespace name from connection string
            string? namespaceName = null;
            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("Endpoint=sb://", StringComparison.OrdinalIgnoreCase))
                {
                    var endpoint = part.Substring("Endpoint=sb://".Length);
                    var dotIndex = endpoint.IndexOf('.');
                    if (dotIndex > 0)
                    {
                        namespaceName = endpoint.Substring(0, dotIndex);
                    }
                    break;
                }
            }

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

        IReadOnlyList<ServiceBusReceivedMessage> messages;

        if (requiresSession)
        {
            var allMessages = new List<ServiceBusReceivedMessage>();
            var sessionsChecked = new HashSet<string>();
            var maxSessions = 10;

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

                        if (subscription != null)
                        {
                            if (deadLetter)
                            {
                                var dlqPath = $"{entityName}/Subscriptions/{subscription}/$DeadLetterQueue";
                                sessionReceiver = await client.AcceptNextSessionAsync(dlqPath, sessionOptions, ct);
                            }
                            else
                            {
                                sessionReceiver = await client.AcceptNextSessionAsync(entityName, subscription, sessionOptions, ct);
                            }
                        }
                        else
                        {
                            if (deadLetter)
                            {
                                var dlqPath = $"{entityName}/$DeadLetterQueue";
                                sessionReceiver = await client.AcceptNextSessionAsync(dlqPath, sessionOptions, ct);
                            }
                            else
                            {
                                sessionReceiver = await client.AcceptNextSessionAsync(entityName, sessionOptions, ct);
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
                        break;
                    }
                }
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                // No sessions available
            }

            messages = allMessages;
        }
        else
        {
            ServiceBusReceiver receiver;
            if (subscription != null)
            {
                var options = deadLetter ? new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter } : null;
                receiver = options != null
                    ? client.CreateReceiver(entityName, subscription, options)
                    : client.CreateReceiver(entityName, subscription);
            }
            else
            {
                var options = deadLetter ? new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter } : null;
                receiver = options != null
                    ? client.CreateReceiver(entityName, options)
                    : client.CreateReceiver(entityName);
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
        ));
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
        var sender = client.CreateSender(entityName);

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

    public async Task PurgeMessagesAsync(
        string connectionString,
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);

        var options = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        };

        if (deadLetter)
            options.SubQueue = SubQueue.DeadLetter;

        ServiceBusReceiver receiver = subscription != null
            ? client.CreateReceiver(entityName, subscription, options)
            : client.CreateReceiver(entityName, options);

        while (!ct.IsCancellationRequested)
        {
            var msgs = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(5), ct);
            if (msgs.Count == 0) break;
        }
    }

    public async Task<(bool IsValid, string? EntityName, string? Endpoint, string? ErrorMessage)> ValidateConnectionStringAsync(
        string connectionString,
        CancellationToken ct = default)
    {
        try
        {
            // Parse connection string to extract endpoint
            string? endpoint = null;
            string? entityPath = null;

            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("Endpoint=sb://", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = part.Substring("Endpoint=sb://".Length).TrimEnd('/');
                }
                else if (part.StartsWith("EntityPath=", StringComparison.OrdinalIgnoreCase))
                {
                    entityPath = part.Substring("EntityPath=".Length);
                }
            }

            if (endpoint == null)
            {
                return (false, null, null, "Invalid connection string format: missing endpoint");
            }

            // Try to connect and validate
            await using var client = new ServiceBusClient(connectionString);
            
            // If EntityPath is specified, it's a queue/topic-specific connection string
            if (!string.IsNullOrEmpty(entityPath))
            {
                return (true, entityPath, endpoint, null);
            }

            // Otherwise, it's a namespace-level connection string
            // Try to list entities to validate it works
            var adminClient = new ServiceBusAdministrationClient(connectionString);
            
            // Just check if we can connect - this will throw if connection string is invalid
            await foreach (var _ in adminClient.GetQueuesAsync(ct))
            {
                break; // We just need to verify we can connect
            }

            return (true, null, endpoint, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }
}

