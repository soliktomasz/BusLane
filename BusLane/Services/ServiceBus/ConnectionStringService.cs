namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BusLane.Models;

public class ConnectionStringService : IConnectionStringService
{
    private const int MaxSessionsToCheck = 10;
    private const int PurgeBatchSize = 100;
    private static readonly TimeSpan PurgeReceiveTimeout = TimeSpan.FromSeconds(5);

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
            ? await PeekSessionMessagesAsync(client, entityName, subscription, count, deadLetter, ct)
            : await PeekStandardMessagesAsync(client, entityName, subscription, count, deadLetter, ct);

        return messages.Select(MapToMessageInfo);
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
        ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
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

        var options = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
            SubQueue = deadLetter ? SubQueue.DeadLetter : SubQueue.None
        };

        await using var receiver = subscription != null
            ? client.CreateReceiver(entityName, subscription, options)
            : client.CreateReceiver(entityName, options);

        while (!ct.IsCancellationRequested)
        {
            var msgs = await receiver.ReceiveMessagesAsync(PurgeBatchSize, PurgeReceiveTimeout, ct);
            if (msgs.Count == 0) break;
        }
    }

    public async Task<int> DeleteMessagesAsync(
        string connectionString,
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);

        var receiverOptions = new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock };
        await using var receiver = subscription != null
            ? client.CreateReceiver(entityName, subscription, receiverOptions)
            : client.CreateReceiver(entityName, receiverOptions);

        var deletedCount = 0;
        var sequenceList = sequenceNumbers.ToList();

        foreach (var sequenceNumber in sequenceList)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var msg = await receiver.ReceiveDeferredMessageAsync(sequenceNumber, ct);
                if (msg != null)
                {
                    await receiver.CompleteMessageAsync(msg, ct);
                    deletedCount++;
                }
            }
            catch (ServiceBusException)
            {
                // Message might not be found or already processed, continue with others
            }
        }

        return deletedCount;
    }

    public async Task<int> ResendMessagesAsync(
        string connectionString,
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender(entityName);

        var sentCount = 0;
        var messageList = messages.ToList();

        // Send messages in batches for better performance
        const int batchSize = 50;
        for (var i = 0; i < messageList.Count; i += batchSize)
        {
            if (ct.IsCancellationRequested) break;

            var batch = messageList.Skip(i).Take(batchSize).ToList();
            var serviceBusMessages = new List<ServiceBusMessage>();

            foreach (var msg in batch)
            {
                var sbMsg = new ServiceBusMessage(msg.Body);
                ApplyMessageProperties(sbMsg, msg.ContentType, msg.CorrelationId, null, msg.SessionId,
                    msg.Subject, msg.To, msg.ReplyTo, msg.ReplyToSessionId, msg.PartitionKey,
                    msg.TimeToLive, null, msg.Properties);
                serviceBusMessages.Add(sbMsg);
            }

            try
            {
                await sender.SendMessagesAsync(serviceBusMessages, ct);
                sentCount += batch.Count;
            }
            catch (ServiceBusException)
            {
                // If batch send fails, try sending individually
                foreach (var sbMsg in serviceBusMessages)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        await sender.SendMessageAsync(sbMsg, ct);
                        sentCount++;
                    }
                    catch (ServiceBusException)
                    {
                        // Individual message failed, continue with others
                    }
                }
            }
        }

        return sentCount;
    }

    public async Task<int> ResubmitDeadLetterMessagesAsync(
        string connectionString,
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);

        // Create receiver for dead letter queue to complete messages
        var receiverOptions = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            SubQueue = SubQueue.DeadLetter
        };
        await using var deadLetterReceiver = subscription != null
            ? client.CreateReceiver(entityName, subscription, receiverOptions)
            : client.CreateReceiver(entityName, receiverOptions);

        // Create sender to send messages back to main queue
        await using var sender = client.CreateSender(entityName);

        var resubmittedCount = 0;
        var messageList = messages.ToList();

        foreach (var msg in messageList)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Receive the deferred message from DLQ
                var dlqMsg = await deadLetterReceiver.ReceiveDeferredMessageAsync(msg.SequenceNumber, ct);
                if (dlqMsg == null) continue;

                // Create new message without dead letter properties
                var newMsg = new ServiceBusMessage(dlqMsg.Body)
                {
                    ContentType = dlqMsg.ContentType,
                    CorrelationId = dlqMsg.CorrelationId,
                    Subject = dlqMsg.Subject,
                    To = dlqMsg.To,
                    ReplyTo = dlqMsg.ReplyTo,
                    ReplyToSessionId = dlqMsg.ReplyToSessionId,
                    SessionId = dlqMsg.SessionId,
                    PartitionKey = dlqMsg.PartitionKey
                };

                // Copy application properties
                foreach (var prop in dlqMsg.ApplicationProperties)
                    newMsg.ApplicationProperties[prop.Key] = prop.Value;

                // Send to main queue
                await sender.SendMessageAsync(newMsg, ct);

                // Complete (remove) from dead letter queue
                await deadLetterReceiver.CompleteMessageAsync(dlqMsg, ct);

                resubmittedCount++;
            }
            catch (ServiceBusException)
            {
                // Message might not be found or already processed, continue with others
            }
        }

        return resubmittedCount;
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

    private static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekStandardMessagesAsync(
        ServiceBusClient client, string entityName, string? subscription,
        int count, bool deadLetter, CancellationToken ct)
    {
        await using var receiver = CreateReceiver(client, entityName, subscription, deadLetter);
        return await receiver.PeekMessagesAsync(count, cancellationToken: ct);
    }

    private static ServiceBusReceiver CreateReceiver(
        ServiceBusClient client, string entityName, string? subscription, bool deadLetter)
    {
        var options = deadLetter ? new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter } : null;

        return subscription != null
            ? (options != null ? client.CreateReceiver(entityName, subscription, options) : client.CreateReceiver(entityName, subscription))
            : (options != null ? client.CreateReceiver(entityName, options) : client.CreateReceiver(entityName));
    }

    private static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekSessionMessagesAsync(
        ServiceBusClient client, string entityName, string? subscription,
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
                        client, entityName, subscription, deadLetter, ct);

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
                    break;
                }
            }
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            // No sessions available at all
        }

        return allMessages;
    }

    private static async Task<ServiceBusSessionReceiver> AcceptNextSessionReceiverAsync(
        ServiceBusClient client, string entityName, string? subscription,
        bool deadLetter, CancellationToken ct)
    {
        var sessionOptions = new ServiceBusSessionReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock };

        if (subscription != null)
        {
            return deadLetter
                ? await client.AcceptNextSessionAsync($"{entityName}/Subscriptions/{subscription}/$DeadLetterQueue", sessionOptions, ct)
                : await client.AcceptNextSessionAsync(entityName, subscription, sessionOptions, ct);
        }

        return deadLetter
            ? await client.AcceptNextSessionAsync($"{entityName}/$DeadLetterQueue", sessionOptions, ct)
            : await client.AcceptNextSessionAsync(entityName, sessionOptions, ct);
    }

    #endregion
}

