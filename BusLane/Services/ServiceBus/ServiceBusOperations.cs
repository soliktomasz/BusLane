namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using BusLane.Models;
using Serilog;

/// <summary>
/// Configuration options for Service Bus batch operations.
/// </summary>
public record ServiceBusOperationOptions
{
    /// <summary>Maximum number of sessions to check when processing session-enabled entities.</summary>
    public int MaxSessionsToCheck { get; init; } = 10;

    /// <summary>Number of messages to process per batch during purge operations.</summary>
    public int PurgeBatchSize { get; init; } = 100;

    /// <summary>Timeout for receiving messages during purge operations.</summary>
    public TimeSpan PurgeReceiveTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Number of messages to process per batch during delete operations.</summary>
    public int DeleteBatchSize { get; init; } = 100;

    /// <summary>Timeout for receiving messages during delete operations.</summary>
    public TimeSpan DeleteReceiveTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Number of consecutive empty batches before stopping an operation.</summary>
    public int MaxEmptyBatches { get; init; } = 3;

    /// <summary>Number of messages to send per batch during resend operations.</summary>
    public int ResendBatchSize { get; init; } = 50;

    /// <summary>Default TTL for queues/topics when not specified by the service.</summary>
    public TimeSpan DefaultMessageTimeToLive { get; init; } = TimeSpan.FromDays(14);

    /// <summary>Default lock duration when not specified by the service.</summary>
    public TimeSpan DefaultLockDuration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Default options instance with standard values.</summary>
    public static ServiceBusOperationOptions Default { get; } = new();
}

/// <summary>
/// Shared operations and utilities for Service Bus services.
/// Used by ConnectionStringOperations and AzureCredentialOperations implementations.
/// </summary>
internal static class ServiceBusOperations
{
    // Default options used by shared operations.
    public static ServiceBusOperationOptions Options { get; } = ServiceBusOperationOptions.Default;

    // Convenience accessors for backwards compatibility
    public static int MaxSessionsToCheck => Options.MaxSessionsToCheck;
    public static int PurgeBatchSize => Options.PurgeBatchSize;
    public static TimeSpan PurgeReceiveTimeout => Options.PurgeReceiveTimeout;
    public static int DeleteBatchSize => Options.DeleteBatchSize;
    public static TimeSpan DeleteReceiveTimeout => Options.DeleteReceiveTimeout;
    public static int MaxEmptyBatches => Options.MaxEmptyBatches;
    public static int ResendBatchSize => Options.ResendBatchSize;

    /// <summary>
    /// Maps a ServiceBusReceivedMessage to our MessageInfo model.
    /// </summary>
    public static MessageInfo MapToMessageInfo(ServiceBusReceivedMessage m) => new(
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

    /// <summary>
    /// Applies message properties to a ServiceBusMessage.
    /// </summary>
    public static void ApplyMessageProperties(
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

    /// <summary>
    /// Creates a ServiceBusReceiver with the specified options.
    /// </summary>
    public static ServiceBusReceiver CreateReceiver(
        ServiceBusClient client, 
        string entityName, 
        string? subscription, 
        bool deadLetter)
    {
        var options = new ServiceBusReceiverOptions();
        if (deadLetter) 
            options.SubQueue = SubQueue.DeadLetter;

        return subscription != null
            ? client.CreateReceiver(entityName, subscription, options)
            : client.CreateReceiver(entityName, options);
    }

    /// <summary>
    /// Peeks messages from a standard (non-session) entity.
    /// </summary>
    public static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekStandardMessagesAsync(
        ServiceBusClient client, 
        string entityName, 
        string? subscription,
        int count, 
        long? fromSequenceNumber,
        bool deadLetter, 
        CancellationToken ct)
    {
        await using var receiver = CreateReceiver(client, entityName, subscription, deadLetter);

        var allMessages = new List<ServiceBusReceivedMessage>(count);
        var nextFromSequenceNumber = fromSequenceNumber;

        while (allMessages.Count < count)
        {
            var remaining = count - allMessages.Count;
            var batch = nextFromSequenceNumber.HasValue
                ? await receiver.PeekMessagesAsync(remaining, nextFromSequenceNumber.Value, cancellationToken: ct)
                : await receiver.PeekMessagesAsync(remaining, cancellationToken: ct);

            if (batch.Count == 0)
            {
                break;
            }

            allMessages.AddRange(batch);

            var lastSequenceNumber = batch[^1].SequenceNumber;
            if (lastSequenceNumber == long.MaxValue)
            {
                break;
            }

            nextFromSequenceNumber = lastSequenceNumber + 1;
        }

        return allMessages;
    }

    /// <summary>
    /// Peeks messages from a session-enabled entity.
    /// </summary>
    public static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekSessionMessagesAsync(
        ServiceBusClient client, 
        string entityName, 
        string? subscription,
        int count,
        long? fromSequenceNumber,
        bool deadLetter, 
        CancellationToken ct)
    {
        var allMessages = new List<ServiceBusReceivedMessage>();
        var sessionsChecked = new HashSet<string>();
        var acquiredSessionReceivers = new List<ServiceBusSessionReceiver>();

        try
        {
            while (allMessages.Count < count && sessionsChecked.Count < Math.Max(MaxSessionsToCheck, count))
            {
                try
                {
                    using var acceptTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    acceptTimeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
                    var sessionReceiver = await AcceptNextSessionReceiverAsync(
                        client, entityName, subscription, deadLetter, acceptTimeoutCts.Token);

                    if (sessionsChecked.Contains(sessionReceiver.SessionId))
                    {
                        await sessionReceiver.DisposeAsync();
                        break;
                    }

                    sessionsChecked.Add(sessionReceiver.SessionId);
                    acquiredSessionReceivers.Add(sessionReceiver);

                    var remaining = count - allMessages.Count;
                    var sessionMessages = fromSequenceNumber.HasValue
                        ? await sessionReceiver.PeekMessagesAsync(remaining, fromSequenceNumber.Value, cancellationToken: ct)
                        : await sessionReceiver.PeekMessagesAsync(remaining, cancellationToken: ct);
                    allMessages.AddRange(sessionMessages);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    break;
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
        finally
        {
            foreach (var receiver in acquiredSessionReceivers)
            {
                try
                {
                    await receiver.DisposeAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        return allMessages;
    }

    /// <summary>
    /// Accepts the next available session receiver.
    /// </summary>
    public static async Task<ServiceBusSessionReceiver> AcceptNextSessionReceiverAsync(
        ServiceBusClient client, 
        string entityName, 
        string? subscription,
        bool deadLetter, 
        CancellationToken ct)
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

    /// <summary>
    /// Deletes multiple messages by sequence numbers.
    /// </summary>
    public static async Task<int> DeleteMessagesAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool deadLetter,
        CancellationToken ct)
    {
        var result = await DeleteMessagesDetailedAsync(
            client,
            entityName,
            subscription,
            sequenceNumbers.Select(s => new MessageIdentifier(s, null)),
            deadLetter,
            ct);

        return result.SucceededCount;
    }

    public static async Task<BulkOperationExecutionResult> DeleteMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<MessageIdentifier> messages,
        bool deadLetter,
        CancellationToken ct)
    {
        var receiverOptions = new ServiceBusReceiverOptions 
        { 
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            SubQueue = deadLetter ? SubQueue.DeadLetter : SubQueue.None
        };
        
        await using var receiver = subscription != null
            ? client.CreateReceiver(entityName, subscription, receiverOptions)
            : client.CreateReceiver(entityName, receiverOptions);

        var requestedMessages = messages.ToList();
        var deletedCount = 0;
        var sequenceSet = requestedMessages.Select(m => m.SequenceNumber).ToHashSet();
        var consecutiveEmptyBatches = 0;

        while (sequenceSet.Count > 0 && consecutiveEmptyBatches < MaxEmptyBatches && !ct.IsCancellationRequested)
        {
            var receivedMessages = await receiver.ReceiveMessagesAsync(DeleteBatchSize, DeleteReceiveTimeout, ct);

            if (receivedMessages.Count == 0)
            {
                consecutiveEmptyBatches++;
                continue;
            }

            consecutiveEmptyBatches = 0;

            foreach (var msg in receivedMessages)
            {
                if (sequenceSet.Contains(msg.SequenceNumber))
                {
                    try
                    {
                        await receiver.CompleteMessageAsync(msg, ct);
                        sequenceSet.Remove(msg.SequenceNumber);
                        deletedCount++;
                    }
                    catch (ServiceBusException ex)
                    {
                        // Message might already be processed or lock expired, continue
                        Log.Debug(ex, "Failed to complete message {SequenceNumber}, continuing", msg.SequenceNumber);
                    }
                }
                else
                {
                    try
                    {
                        await receiver.AbandonMessageAsync(msg, cancellationToken: ct);
                    }
                    catch (ServiceBusException ex)
                    {
                        // Ignore abandon errors
                        Log.Debug(ex, "Failed to abandon message {SequenceNumber}, ignoring", msg.SequenceNumber);
                    }
                }
            }
        }

        var failed = requestedMessages
            .Where(m => sequenceSet.Contains(m.SequenceNumber))
            .ToList();

        return new BulkOperationExecutionResult(
            BulkOperationType.Delete,
            requestedMessages.Count,
            deletedCount,
            failed,
            $"Deleted {deletedCount} of {requestedMessages.Count} message(s)",
            CanResume: failed.Count > 0);
    }

    /// <summary>
    /// Resends multiple messages to an entity.
    /// </summary>
    public static async Task<int> ResendMessagesAsync(
        ServiceBusClient client,
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct)
    {
        var result = await ResendMessagesDetailedAsync(client, entityName, messages, ct);
        return result.SucceededCount;
    }

    public static async Task<BulkOperationExecutionResult> ResendMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct)
    {
        await using var sender = client.CreateSender(entityName);

        var sentCount = 0;
        // Avoid allocation if already a list
        var messageList = messages as IList<MessageInfo> ?? messages.ToList();
        var failed = new List<MessageIdentifier>();

        for (var i = 0; i < messageList.Count; i += ResendBatchSize)
        {
            if (ct.IsCancellationRequested) break;

            // Calculate batch bounds to avoid Skip/Take allocations
            var batchSize = Math.Min(ResendBatchSize, messageList.Count - i);
            var serviceBusMessages = new List<ServiceBusMessage>(batchSize);
            var sourceBatch = new List<MessageInfo>(batchSize);

            for (var j = 0; j < batchSize; j++)
            {
                var msg = messageList[i + j];
                sourceBatch.Add(msg);
                var sbMsg = new ServiceBusMessage(msg.Body);
                ApplyMessageProperties(sbMsg, msg.ContentType, msg.CorrelationId, null, msg.SessionId,
                    msg.Subject, msg.To, msg.ReplyTo, msg.ReplyToSessionId, msg.PartitionKey,
                    msg.TimeToLive, null, msg.Properties);
                serviceBusMessages.Add(sbMsg);
            }

            try
            {
                await sender.SendMessagesAsync(serviceBusMessages, ct);
                sentCount += batchSize;
            }
            catch (ServiceBusException ex)
            {
                // If batch send fails, try sending individually
                Log.Debug(ex, "Batch send failed, attempting individual message sends");
                for (var index = 0; index < serviceBusMessages.Count; index++)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var sbMsg = serviceBusMessages[index];
                        await sender.SendMessageAsync(sbMsg, ct);
                        sentCount++;
                    }
                    catch (ServiceBusException innerEx)
                    {
                        // Individual message failed, continue with others
                        Log.Debug(innerEx, "Failed to send individual message, continuing with remaining messages");
                        var failedMessage = sourceBatch[index];
                        failed.Add(new MessageIdentifier(failedMessage.SequenceNumber, failedMessage.MessageId));
                    }
                }
            }
        }

        return new BulkOperationExecutionResult(
            BulkOperationType.Resend,
            messageList.Count,
            sentCount,
            failed,
            $"Resent {sentCount} of {messageList.Count} message(s)",
            CanResume: failed.Count > 0);
    }

    /// <summary>
    /// Resubmits messages from dead letter queue back to main queue.
    /// </summary>
    public static async Task<int> ResubmitDeadLetterMessagesAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct)
    {
        var result = await ResubmitDeadLetterMessagesDetailedAsync(client, entityName, subscription, messages, ct);
        return result.SucceededCount;
    }

    public static async Task<BulkOperationExecutionResult> ResubmitDeadLetterMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct)
    {
        var receiverOptions = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            SubQueue = SubQueue.DeadLetter
        };

        await using var deadLetterReceiver = subscription != null
            ? client.CreateReceiver(entityName, subscription, receiverOptions)
            : client.CreateReceiver(entityName, receiverOptions);

        await using var sender = client.CreateSender(entityName);

        var resubmittedCount = 0;
        var messageList = messages.ToList();
        var failed = new List<MessageIdentifier>();

        // Iterate directly without materializing to list - we only enumerate once
        foreach (var msg in messageList)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var dlqMsg = await deadLetterReceiver.ReceiveDeferredMessageAsync(msg.SequenceNumber, ct);
                if (dlqMsg == null)
                {
                    failed.Add(new MessageIdentifier(msg.SequenceNumber, msg.MessageId));
                    continue;
                }

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

                foreach (var prop in dlqMsg.ApplicationProperties)
                    newMsg.ApplicationProperties[prop.Key] = prop.Value;

                await sender.SendMessageAsync(newMsg, ct);
                await deadLetterReceiver.CompleteMessageAsync(dlqMsg, ct);

                resubmittedCount++;
            }
            catch (ServiceBusException ex)
            {
                // Message might not be found or already processed, continue
                Log.Debug(ex, "Failed to resubmit message {SequenceNumber} from DLQ, continuing", msg.SequenceNumber);
                failed.Add(new MessageIdentifier(msg.SequenceNumber, msg.MessageId));
            }
        }

        return new BulkOperationExecutionResult(
            BulkOperationType.ResubmitDeadLetter,
            messageList.Count,
            resubmittedCount,
            failed,
            $"Resubmitted {resubmittedCount} of {messageList.Count} message(s)",
            CanResume: failed.Count > 0);
    }

    /// <summary>
    /// Purges all messages from an entity.
    /// </summary>
    public static async Task PurgeMessagesAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct)
    {
        _ = await PurgeMessagesDetailedAsync(client, entityName, subscription, deadLetter, ct);
    }

    public static async Task<BulkOperationExecutionResult> PurgeMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct)
    {
        var options = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
            SubQueue = deadLetter ? SubQueue.DeadLetter : SubQueue.None
        };

        await using var receiver = subscription != null
            ? client.CreateReceiver(entityName, subscription, options)
            : client.CreateReceiver(entityName, options);

        var deletedCount = 0;
        while (!ct.IsCancellationRequested)
        {
            var msgs = await receiver.ReceiveMessagesAsync(PurgeBatchSize, PurgeReceiveTimeout, ct);
            if (msgs.Count == 0) break;
            deletedCount += msgs.Count;
        }

        return new BulkOperationExecutionResult(
            BulkOperationType.Purge,
            deletedCount,
            deletedCount,
            [],
            deletedCount == 0 ? "No messages found to purge" : $"Purged {deletedCount} message(s)");
    }
}
