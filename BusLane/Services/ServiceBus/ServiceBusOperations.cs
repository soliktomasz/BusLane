namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
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

    /// <summary>Timeout for receiving peek-lock messages.</summary>
    public TimeSpan ReceiveTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Number of consecutive empty batches before stopping an operation.</summary>
    public int MaxEmptyBatches { get; init; } = 3;

    /// <summary>Maximum number of messages scanned while locating selected messages.</summary>
    public int MaxSelectedMessageScanCount { get; init; } = 1000;

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
    public static TimeSpan ReceiveTimeout => Options.ReceiveTimeout;
    public static int MaxEmptyBatches => Options.MaxEmptyBatches;
    public static int MaxSelectedMessageScanCount => Options.MaxSelectedMessageScanCount;
    public static int ResendBatchSize => Options.ResendBatchSize;

    internal static readonly TimeSpan SessionAcceptTimeout = TimeSpan.FromSeconds(5);
    internal static readonly int SessionInspectorPeekBatchSize = 100;

    internal record SessionMessageSnapshot(
        string SessionId,
        long MessageCount,
        DateTimeOffset? LastActivityAt,
        DateTimeOffset? LockedUntil,
        string? State);

    /// <summary>
    /// Builds Azure SDK queue creation options from BusLane creation options.
    /// </summary>
    public static CreateQueueOptions BuildCreateQueueOptions(QueueCreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new ArgumentException("Queue name is required.", nameof(QueueCreationOptions.Name));
        }

        var sdkOptions = new CreateQueueOptions(options.Name)
        {
            RequiresSession = options.RequiresSession
        };
        if (options.DefaultMessageTimeToLive.HasValue)
        {
            sdkOptions.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive.Value;
        }

        if (options.LockDuration.HasValue)
        {
            sdkOptions.LockDuration = options.LockDuration.Value;
        }

        if (options.DuplicateDetectionHistoryTimeWindow.HasValue)
        {
            sdkOptions.RequiresDuplicateDetection = true;
            sdkOptions.DuplicateDetectionHistoryTimeWindow = options.DuplicateDetectionHistoryTimeWindow.Value;
        }

        if (options.MaxSizeInMegabytes.HasValue)
        {
            sdkOptions.MaxSizeInMegabytes = options.MaxSizeInMegabytes.Value;
        }

        if (options.EnablePartitioning.HasValue)
        {
            sdkOptions.EnablePartitioning = options.EnablePartitioning.Value;
        }

        if (options.EnableBatchedOperations.HasValue)
        {
            sdkOptions.EnableBatchedOperations = options.EnableBatchedOperations.Value;
        }

        return sdkOptions;
    }

    /// <summary>
    /// Builds Azure SDK topic creation options from BusLane creation options.
    /// </summary>
    public static CreateTopicOptions BuildCreateTopicOptions(TopicCreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new ArgumentException("Topic name is required.", nameof(TopicCreationOptions.Name));
        }

        var sdkOptions = new CreateTopicOptions(options.Name);
        if (options.DefaultMessageTimeToLive.HasValue)
        {
            sdkOptions.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive.Value;
        }

        if (options.DuplicateDetectionHistoryTimeWindow.HasValue)
        {
            sdkOptions.RequiresDuplicateDetection = true;
            sdkOptions.DuplicateDetectionHistoryTimeWindow = options.DuplicateDetectionHistoryTimeWindow.Value;
        }

        if (options.MaxSizeInMegabytes.HasValue)
        {
            sdkOptions.MaxSizeInMegabytes = options.MaxSizeInMegabytes.Value;
        }

        if (options.EnablePartitioning.HasValue)
        {
            sdkOptions.EnablePartitioning = options.EnablePartitioning.Value;
        }

        if (options.EnableBatchedOperations.HasValue)
        {
            sdkOptions.EnableBatchedOperations = options.EnableBatchedOperations.Value;
        }

        return sdkOptions;
    }

    /// <summary>
    /// Builds Azure SDK subscription creation options from BusLane creation options.
    /// </summary>
    public static CreateSubscriptionOptions BuildCreateSubscriptionOptions(
        string topicName,
        SubscriptionCreationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new ArgumentException("Subscription name is required.", nameof(SubscriptionCreationOptions.Name));
        }

        return new CreateSubscriptionOptions(topicName, options.Name)
        {
            RequiresSession = options.RequiresSession
        };
    }

    /// <summary>
    /// Maps Azure SDK rule properties into the display model used by BusLane.
    /// </summary>
    public static SubscriptionRuleInfo MapToSubscriptionRuleInfo(RuleProperties rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var actionExpression = rule.Action is SqlRuleAction sqlAction
            ? sqlAction.SqlExpression
            : null;

        return rule.Filter switch
        {
            TrueRuleFilter => new SubscriptionRuleInfo(
                rule.Name,
                SubscriptionRuleFilterType.True,
                "True",
                ActionExpression: actionExpression),

            SqlRuleFilter sqlFilter => new SubscriptionRuleInfo(
                rule.Name,
                SubscriptionRuleFilterType.Sql,
                sqlFilter.SqlExpression,
                sqlFilter.SqlExpression,
                actionExpression),

            CorrelationRuleFilter correlationFilter => BuildCorrelationRuleInfo(rule.Name, correlationFilter, actionExpression),

            _ => new SubscriptionRuleInfo(
                rule.Name,
                SubscriptionRuleFilterType.Sql,
                rule.Filter.ToString() ?? string.Empty,
                ActionExpression: actionExpression)
        };
    }

    /// <summary>
    /// Builds Azure SDK rule creation options from BusLane creation options.
    /// </summary>
    public static CreateRuleOptions BuildCreateRuleOptions(SubscriptionRuleCreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new ArgumentException("Rule name is required.", nameof(SubscriptionRuleCreationOptions.Name));
        }

        RuleFilter filter = options.FilterType switch
        {
            SubscriptionRuleFilterType.True => new TrueRuleFilter(),
            SubscriptionRuleFilterType.Sql => BuildSqlRuleFilter(options),
            SubscriptionRuleFilterType.Correlation => BuildCorrelationRuleFilter(options),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.FilterType, "Unsupported subscription rule filter type.")
        };

        var ruleOptions = new CreateRuleOptions(options.Name, filter);
        if (!string.IsNullOrWhiteSpace(options.ActionExpression))
        {
            ruleOptions.Action = new SqlRuleAction(options.ActionExpression);
        }

        return ruleOptions;
    }

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

    public static SessionInspectorItem BuildSessionInspectorItem(
        SessionMessageSnapshot? activeSnapshot,
        SessionMessageSnapshot? deadLetterSnapshot)
    {
        var sessionId = activeSnapshot?.SessionId ?? deadLetterSnapshot?.SessionId;
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var lastActivity = MaxDate(activeSnapshot?.LastActivityAt, deadLetterSnapshot?.LastActivityAt);
        var lockedUntil = MaxDate(activeSnapshot?.LockedUntil, deadLetterSnapshot?.LockedUntil);
        var state = activeSnapshot?.State ?? deadLetterSnapshot?.State;

        return new SessionInspectorItem(
            sessionId,
            activeSnapshot?.MessageCount ?? 0,
            deadLetterSnapshot?.MessageCount ?? 0,
            lastActivity,
            lockedUntil,
            state);
    }

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

    public static async Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveMessagesAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        int count,
        bool deadLetter,
        bool requiresSession,
        string? sessionId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        if (count <= 0)
        {
            return [];
        }

        if (requiresSession)
        {
            var sessionReceiver = string.IsNullOrWhiteSpace(sessionId)
                ? await AcceptNextSessionReceiverAsync(client, entityName, subscription, deadLetter, ct)
                : await AcceptSessionReceiverAsync(client, entityName, subscription, sessionId, deadLetter, ct);

            try
            {
                var sessionMessages = await sessionReceiver.ReceiveMessagesAsync(
                    count,
                    maxWaitTime: ReceiveTimeout,
                    cancellationToken: ct);
                if (sessionMessages.Count == 0)
                {
                    await sessionReceiver.DisposeAsync();
                    return [];
                }

                var lease = new SessionReceiverLease(sessionReceiver, sessionMessages.Count);
                return sessionMessages
                    .Select(message => ToReceivedMessageInfo(message, entityName, subscription, deadLetter, requiresSession, sessionReceiver.SessionId, lease))
                    .ToList();
            }
            catch
            {
                await sessionReceiver.DisposeAsync();
                throw;
            }
        }

        await using var receiver = CreateReceiver(client, entityName, subscription, deadLetter);
        var messages = await receiver.ReceiveMessagesAsync(
            count,
            maxWaitTime: ReceiveTimeout,
            cancellationToken: ct);
        return messages
            .Select(message => ToReceivedMessageInfo(message, entityName, subscription, deadLetter, requiresSession, null))
            .ToList();
    }

    public static async Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveDeferredMessagesAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool requiresSession,
        string? sessionId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        var sequenceList = sequenceNumbers.Distinct().ToList();
        if (sequenceList.Count == 0)
        {
            return [];
        }

        if (requiresSession)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            var sessionReceiver = await AcceptSessionReceiverAsync(client, entityName, subscription, sessionId, deadLetter: false, ct);
            try
            {
                var deferredSessionMessages = await sessionReceiver.ReceiveDeferredMessagesAsync(sequenceList, ct);
                if (deferredSessionMessages.Count == 0)
                {
                    await sessionReceiver.DisposeAsync();
                    return [];
                }

                var lease = new SessionReceiverLease(sessionReceiver, deferredSessionMessages.Count);
                return deferredSessionMessages
                    .Select(message => ToReceivedMessageInfo(message, entityName, subscription, deadLetter: false, requiresSession, sessionId, lease))
                    .ToList();
            }
            catch
            {
                await sessionReceiver.DisposeAsync();
                throw;
            }
        }

        await using var receiver = CreateReceiver(client, entityName, subscription, deadLetter: false);
        var deferredMessages = await receiver.ReceiveDeferredMessagesAsync(sequenceList, ct);
        return deferredMessages
            .Select(message => ToReceivedMessageInfo(message, entityName, subscription, deadLetter: false, requiresSession, null))
            .ToList();
    }

    public static async Task CompleteMessageAsync(ServiceBusClient client, ReceivedMessageInfo message, CancellationToken ct)
    {
        await SettleMessageAsync(client, message, receiver => receiver.CompleteMessageAsync(message.ReceivedMessage, ct), ct);
    }

    public static async Task AbandonMessageAsync(ServiceBusClient client, ReceivedMessageInfo message, CancellationToken ct)
    {
        await SettleMessageAsync(client, message, receiver => receiver.AbandonMessageAsync(message.ReceivedMessage, cancellationToken: ct), ct);
    }

    public static async Task DeadLetterMessageAsync(
        ServiceBusClient client,
        ReceivedMessageInfo message,
        string? reason,
        string? description,
        CancellationToken ct)
    {
        await SettleMessageAsync(client, message, receiver => receiver.DeadLetterMessageAsync(message.ReceivedMessage, reason, description, ct), ct);
    }

    public static async Task DeferMessageAsync(ServiceBusClient client, ReceivedMessageInfo message, CancellationToken ct)
    {
        await SettleMessageAsync(client, message, receiver => receiver.DeferMessageAsync(message.ReceivedMessage, cancellationToken: ct), ct);
    }

    public static async Task<DateTimeOffset> RenewMessageLockAsync(ServiceBusClient client, ReceivedMessageInfo message, CancellationToken ct)
    {
        if (message.SessionReceiverLease != null)
        {
            await message.SessionReceiverLease.Receiver.RenewMessageLockAsync(message.ReceivedMessage, ct);
            return message.ReceivedMessage.LockedUntil;
        }

        await using var receiver = await CreateSettlementReceiverAsync(client, message, ct);
        await receiver.RenewMessageLockAsync(message.ReceivedMessage, ct);
        return message.ReceivedMessage.LockedUntil;
    }

    public static ReceivedMessageInfo ToReceivedMessageInfo(
        ServiceBusReceivedMessage message,
        string entityName,
        string? subscription,
        bool deadLetter,
        bool requiresSession,
        string? sessionId,
        SessionReceiverLease? sessionReceiverLease = null) =>
        new(MapToMessageInfo(message), message, entityName, subscription, deadLetter, requiresSession, sessionId)
        {
            SessionReceiverLease = sessionReceiverLease
        };

    private static async Task SettleMessageAsync(
        ServiceBusClient client,
        ReceivedMessageInfo message,
        Func<ServiceBusReceiver, Task> settle,
        CancellationToken ct)
    {
        if (message.SessionReceiverLease != null)
        {
            await settle(message.SessionReceiverLease.Receiver);
            await message.SessionReceiverLease.ReleaseSettlementAsync();
            return;
        }

        await using var receiver = await CreateSettlementReceiverAsync(client, message, ct);
        await settle(receiver);
    }

    private static async Task<ServiceBusReceiver> CreateSettlementReceiverAsync(
        ServiceBusClient client,
        ReceivedMessageInfo message,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(message);
        if (message.RequiresSession)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message.SessionId);
            return await AcceptSessionReceiverAsync(
                client,
                message.EntityName,
                message.SubscriptionName,
                message.SessionId,
                message.DeadLetter,
                ct);
        }

        return CreateReceiver(client, message.EntityName, message.SubscriptionName, message.DeadLetter);
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
        // Azure Service Bus does not allow session receivers against subqueues.
        if (deadLetter)
        {
            return await PeekStandardMessagesAsync(client, entityName, subscription, count, fromSequenceNumber, deadLetter, ct);
        }

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

    public static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekSessionMessagesAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        string sessionId,
        int count,
        long? fromSequenceNumber,
        bool deadLetter,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Azure Service Bus does not allow session receivers against subqueues.
        if (deadLetter)
        {
            var messages = await PeekStandardMessagesAsync(client, entityName, subscription, count, fromSequenceNumber, deadLetter, ct);
            return messages.Where(m => string.Equals(m.SessionId, sessionId, StringComparison.Ordinal)).ToList();
        }

        await using var receiver = await AcceptSessionReceiverAsync(client, entityName, subscription, sessionId, deadLetter, ct);

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

    public static async Task<IReadOnlyList<SessionInspectorItem>> GetSessionInspectorItemsAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        CancellationToken ct)
    {
        var maxSessions = Math.Max(MaxSessionsToCheck, 25);
        var activeSessions = await DiscoverSessionIdsAsync(client, entityName, subscription, deadLetter: false, maxSessions, ct);
        var deadLetterSessions = await DiscoverSessionIdsAsync(client, entityName, subscription, deadLetter: true, maxSessions, ct);
        var allSessionIds = activeSessions.Concat(deadLetterSessions).Distinct(StringComparer.Ordinal).ToList();

        var items = new List<SessionInspectorItem>(allSessionIds.Count);
        foreach (var sessionId in allSessionIds)
        {
            ct.ThrowIfCancellationRequested();

            var activeSnapshot = await GetSessionSnapshotAsync(client, entityName, subscription, sessionId, deadLetter: false, ct);
            var deadLetterSnapshot = await GetSessionSnapshotAsync(client, entityName, subscription, sessionId, deadLetter: true, ct);

            if (activeSnapshot == null && deadLetterSnapshot == null)
            {
                Log.Debug("Skipping session {SessionId} because it was drained before snapshotting completed", sessionId);
                continue;
            }

            items.Add(BuildSessionInspectorItem(activeSnapshot, deadLetterSnapshot));
        }

        return items
            .OrderByDescending(static session => session.LastActivityAt ?? DateTimeOffset.MinValue)
            .ThenBy(static session => session.SessionId, StringComparer.Ordinal)
            .ToList();
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

    public static async Task<ServiceBusSessionReceiver> AcceptSessionReceiverAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        string sessionId,
        bool deadLetter,
        CancellationToken ct)
    {
        var sessionOptions = new ServiceBusSessionReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock };

        if (subscription != null)
        {
            return deadLetter
                ? await client.AcceptSessionAsync($"{entityName}/Subscriptions/{subscription}/$DeadLetterQueue", sessionId, sessionOptions, ct)
                : await client.AcceptSessionAsync(entityName, subscription, sessionId, sessionOptions, ct);
        }

        return deadLetter
            ? await client.AcceptSessionAsync($"{entityName}/$DeadLetterQueue", sessionId, sessionOptions, ct)
            : await client.AcceptSessionAsync(entityName, sessionId, sessionOptions, ct);
    }

    private static async Task<HashSet<string>> DiscoverSessionIdsAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        bool deadLetter,
        int maxSessions,
        CancellationToken ct)
    {
        var sessionIds = new HashSet<string>(StringComparer.Ordinal);
        var acquiredReceivers = new List<ServiceBusSessionReceiver>();

        try
        {
            while (sessionIds.Count < maxSessions)
            {
                try
                {
                    using var acceptTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    acceptTimeoutCts.CancelAfter(SessionAcceptTimeout);
                    var receiver = await AcceptNextSessionReceiverAsync(client, entityName, subscription, deadLetter, acceptTimeoutCts.Token);

                    if (!sessionIds.Add(receiver.SessionId))
                    {
                        await receiver.DisposeAsync();
                        break;
                    }

                    acquiredReceivers.Add(receiver);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ServiceBusException ex) when (ex.Reason is ServiceBusFailureReason.ServiceTimeout or ServiceBusFailureReason.SessionCannotBeLocked)
                {
                    break;
                }
            }
        }
        finally
        {
            foreach (var receiver in acquiredReceivers)
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

        return sessionIds;
    }

    private static async Task<SessionMessageSnapshot?> GetSessionSnapshotAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        string sessionId,
        bool deadLetter,
        CancellationToken ct)
    {
        try
        {
            using var acceptTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            acceptTimeoutCts.CancelAfter(SessionAcceptTimeout);

            await using var receiver = await AcceptSessionReceiverAsync(
                client,
                entityName,
                subscription,
                sessionId,
                deadLetter,
                acceptTimeoutCts.Token);

            var state = await receiver.GetSessionStateAsync(ct);
            var count = 0L;
            DateTimeOffset? lastActivity = null;
            long? nextFromSequenceNumber = null;

            while (true)
            {
                var batch = nextFromSequenceNumber.HasValue
                    ? await receiver.PeekMessagesAsync(SessionInspectorPeekBatchSize, nextFromSequenceNumber.Value, cancellationToken: ct)
                    : await receiver.PeekMessagesAsync(SessionInspectorPeekBatchSize, cancellationToken: ct);

                if (batch.Count == 0)
                {
                    break;
                }

                count += batch.Count;
                var batchLastActivity = batch.Max(static message => message.EnqueuedTime);
                lastActivity = MaxDate(lastActivity, batchLastActivity);

                var lastSequenceNumber = batch[^1].SequenceNumber;
                if (lastSequenceNumber == long.MaxValue)
                {
                    break;
                }

                nextFromSequenceNumber = lastSequenceNumber + 1;
            }

            return new SessionMessageSnapshot(
                sessionId,
                count,
                lastActivity,
                receiver.SessionLockedUntil,
                FormatSessionState(state));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (ServiceBusException ex) when (ex.Reason is ServiceBusFailureReason.ServiceTimeout or ServiceBusFailureReason.SessionCannotBeLocked)
        {
            return null;
        }
    }

    private static string? FormatSessionState(BinaryData? state)
    {
        if (state == null || state.ToMemory().IsEmpty)
        {
            return null;
        }

        try
        {
            return state.ToString();
        }
        catch
        {
            return Convert.ToBase64String(state.ToArray());
        }
    }

    private static DateTimeOffset? MaxDate(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return left.Value >= right.Value ? left : right;
    }

    private static SubscriptionRuleInfo BuildCorrelationRuleInfo(
        string ruleName,
        CorrelationRuleFilter filter,
        string? actionExpression)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(properties, nameof(filter.CorrelationId), filter.CorrelationId);
        AddIfPresent(properties, nameof(filter.MessageId), filter.MessageId);
        AddIfPresent(properties, nameof(filter.To), filter.To);
        AddIfPresent(properties, nameof(filter.ReplyTo), filter.ReplyTo);
        AddIfPresent(properties, nameof(filter.Subject), filter.Subject);
        AddIfPresent(properties, nameof(filter.SessionId), filter.SessionId);
        AddIfPresent(properties, nameof(filter.ReplyToSessionId), filter.ReplyToSessionId);
        AddIfPresent(properties, nameof(filter.ContentType), filter.ContentType);

        foreach (var property in filter.ApplicationProperties)
        {
            if (property.Value != null)
            {
                properties.TryAdd(property.Key, property.Value.ToString() ?? string.Empty);
            }
        }

        var display = properties.Count == 0
            ? "Correlation"
            : string.Join(", ", properties.Select(static p => $"{p.Key} = {p.Value}"));

        return new SubscriptionRuleInfo(
            ruleName,
            SubscriptionRuleFilterType.Correlation,
            display,
            ActionExpression: actionExpression,
            CorrelationProperties: properties);
    }

    private static SqlRuleFilter BuildSqlRuleFilter(SubscriptionRuleCreationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SqlExpression))
        {
            throw new ArgumentException("SQL expression is required.", nameof(SubscriptionRuleCreationOptions.SqlExpression));
        }

        return new SqlRuleFilter(options.SqlExpression);
    }

    private static CorrelationRuleFilter BuildCorrelationRuleFilter(SubscriptionRuleCreationOptions options)
    {
        if (options.CorrelationProperties == null || options.CorrelationProperties.Count == 0)
        {
            throw new ArgumentException("At least one correlation property is required.", nameof(SubscriptionRuleCreationOptions.CorrelationProperties));
        }

        var filter = new CorrelationRuleFilter();
        foreach (var (key, value) in options.CorrelationProperties)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case nameof(CorrelationRuleFilter.CorrelationId):
                    filter.CorrelationId = value;
                    break;
                case nameof(CorrelationRuleFilter.MessageId):
                    filter.MessageId = value;
                    break;
                case nameof(CorrelationRuleFilter.To):
                    filter.To = value;
                    break;
                case nameof(CorrelationRuleFilter.ReplyTo):
                    filter.ReplyTo = value;
                    break;
                case nameof(CorrelationRuleFilter.Subject):
                    filter.Subject = value;
                    break;
                case nameof(CorrelationRuleFilter.SessionId):
                    filter.SessionId = value;
                    break;
                case nameof(CorrelationRuleFilter.ReplyToSessionId):
                    filter.ReplyToSessionId = value;
                    break;
                case nameof(CorrelationRuleFilter.ContentType):
                    filter.ContentType = value;
                    break;
                default:
                    filter.ApplicationProperties[key] = value;
                    break;
            }
        }

        return filter;
    }

    private static void AddIfPresent(IDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
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
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        var result = await DeleteMessagesDetailedAsync(
            client,
            entityName,
            subscription,
            sequenceNumbers.Select(sequenceNumber => new MessageIdentifier(sequenceNumber, null, sessionId)),
            deadLetter,
            requiresSession: requiresSession,
            ct: ct);

        return result.SucceededCount;
    }

    public static async Task<BulkOperationExecutionResult> DeleteMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<MessageIdentifier> messages,
        bool deadLetter,
        IProgress<BulkOperationProgress>? progress = null,
        bool requiresSession = false,
        CancellationToken ct = default)
    {
        var requestedMessages = messages.ToList();
        requiresSession |= requestedMessages.Any(message => !string.IsNullOrWhiteSpace(message.SessionId));
        var succeeded = new HashSet<MessageIdentifier>();
        var failureExceptions = new Dictionary<MessageIdentifier, ServiceBusException>();
        var explicitFailures = new Dictionary<MessageIdentifier, BulkOperationFailure>();

        if (requiresSession)
        {
            foreach (var identifier in requestedMessages.Where(message => string.IsNullOrWhiteSpace(message.SessionId)))
            {
                explicitFailures[identifier] = new BulkOperationFailure(
                    identifier,
                    BulkOperationFailureKind.NonRetryable,
                    "Session ID is required for selected deletion on a session-enabled entity");
            }

            foreach (var sessionGroup in requestedMessages
                         .Where(message => !string.IsNullOrWhiteSpace(message.SessionId))
                         .GroupBy(message => message.SessionId!, StringComparer.Ordinal))
            {
                try
                {
                    await using var receiver = await AcceptSessionReceiverAsync(
                        client,
                        entityName,
                        subscription,
                        sessionGroup.Key,
                        deadLetter,
                        ct);
                    await ProcessSelectedMessagesAsync(
                        receiver,
                        sessionGroup.ToList(),
                        BulkOperationType.Delete,
                        requestedMessages.Count,
                        "Deleted",
                        static (activeReceiver, message, token) => activeReceiver.CompleteMessageAsync(message, token),
                        succeeded,
                        failureExceptions,
                        progress,
                        ct);
                }
                catch (ServiceBusException ex)
                {
                    foreach (var identifier in sessionGroup)
                    {
                        failureExceptions[identifier] = ex;
                    }
                }
            }
        }
        else
        {
            var receiverOptions = new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = deadLetter ? SubQueue.DeadLetter : SubQueue.None
            };

            await using var receiver = subscription != null
                ? client.CreateReceiver(entityName, subscription, receiverOptions)
                : client.CreateReceiver(entityName, receiverOptions);
            await ProcessSelectedMessagesAsync(
                receiver,
                requestedMessages,
                BulkOperationType.Delete,
                requestedMessages.Count,
                "Deleted",
                static (activeReceiver, message, token) => activeReceiver.CompleteMessageAsync(message, token),
                succeeded,
                failureExceptions,
                progress,
                ct);
        }

        var failed = requestedMessages
            .Where(identifier => !succeeded.Contains(identifier))
            .ToList();
        var failures = failed
            .Select(identifier => explicitFailures.TryGetValue(identifier, out var explicitFailure)
                ? explicitFailure
                : failureExceptions.TryGetValue(identifier, out var ex)
                    ? CreateFailure(identifier, ex)
                    : new BulkOperationFailure(identifier, BulkOperationFailureKind.Retryable, "Message was not completed before the operation finished"))
            .ToList();

        return new BulkOperationExecutionResult(
            BulkOperationType.Delete,
            requestedMessages.Count,
            succeeded.Count,
            failed,
            $"Deleted {succeeded.Count} of {requestedMessages.Count} message(s)",
            CanResume: failures.Any(f => f.Kind == BulkOperationFailureKind.Retryable),
            Failures: failures,
            CompletionStatus: ct.IsCancellationRequested ? BulkOperationCompletionStatus.Cancelled : null);
    }

    /// <summary>
    /// Resends multiple messages to an entity.
    /// </summary>
    public static async Task<int> ResendMessagesAsync(
        ServiceBusClient client,
        string entityName,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default)
    {
        var result = await ResendMessagesDetailedAsync(client, entityName, messages, ct: ct);
        return result.SucceededCount;
    }

    public static async Task<BulkOperationExecutionResult> ResendMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        IEnumerable<MessageInfo> messages,
        IProgress<BulkOperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        await using var sender = client.CreateSender(entityName);

        var sentCount = 0;
        // Avoid allocation if already a list
        var messageList = messages as IList<MessageInfo> ?? messages.ToList();
        var failed = new List<MessageIdentifier>();
        var failures = new List<BulkOperationFailure>();
        var processedCount = 0;

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
                processedCount += batchSize;
                progress?.Report(new BulkOperationProgress(
                    BulkOperationType.Resend,
                    processedCount,
                    messageList.Count,
                    $"Processed {processedCount} of {messageList.Count} message(s)"));
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
                        processedCount++;
                        progress?.Report(new BulkOperationProgress(
                            BulkOperationType.Resend,
                            processedCount,
                            messageList.Count,
                            $"Processed {processedCount} of {messageList.Count} message(s)"));
                    }
                    catch (ServiceBusException innerEx)
                    {
                        // Individual message failed, continue with others
                        Log.Debug(innerEx, "Failed to send individual message, continuing with remaining messages");
                        var failedMessage = sourceBatch[index];
                        var identifier = new MessageIdentifier(failedMessage.SequenceNumber, failedMessage.MessageId);
                        failed.Add(identifier);
                        failures.Add(CreateFailure(identifier, innerEx));
                        processedCount++;
                        progress?.Report(new BulkOperationProgress(
                            BulkOperationType.Resend,
                            processedCount,
                            messageList.Count,
                            $"Processed {processedCount} of {messageList.Count} message(s)"));
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
            CanResume: failures.Any(f => f.Kind == BulkOperationFailureKind.Retryable),
            Failures: failures,
            CompletionStatus: ct.IsCancellationRequested ? BulkOperationCompletionStatus.Cancelled : null);
    }

    /// <summary>
    /// Resubmits messages from dead letter queue back to main queue.
    /// </summary>
    public static async Task<int> ResubmitDeadLetterMessagesAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        CancellationToken ct = default)
    {
        var result = await ResubmitDeadLetterMessagesDetailedAsync(client, entityName, subscription, messages, ct: ct);
        return result.SucceededCount;
    }

    public static async Task<BulkOperationExecutionResult> ResubmitDeadLetterMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        IProgress<BulkOperationProgress>? progress = null,
        bool requiresSession = false,
        CancellationToken ct = default)
    {
        var messageList = messages.ToList();
        var requestedMessages = messageList
            .Select(message => new MessageIdentifier(message.SequenceNumber, message.MessageId, message.SessionId))
            .ToList();
        requiresSession |= requestedMessages.Any(message => !string.IsNullOrWhiteSpace(message.SessionId));
        var succeeded = new HashSet<MessageIdentifier>();
        var failureExceptions = new Dictionary<MessageIdentifier, ServiceBusException>();
        var explicitFailures = new Dictionary<MessageIdentifier, BulkOperationFailure>();

        await using var sender = client.CreateSender(entityName);

        async Task ResubmitAsync(ServiceBusReceiver receiver, ServiceBusReceivedMessage deadLetterMessage, CancellationToken token)
        {
            var newMessage = CreateResubmittedMessage(deadLetterMessage);
            await sender.SendMessageAsync(newMessage, token);
            await receiver.CompleteMessageAsync(deadLetterMessage, token);
        }

        if (requiresSession)
        {
            foreach (var identifier in requestedMessages.Where(message => string.IsNullOrWhiteSpace(message.SessionId)))
            {
                explicitFailures[identifier] = new BulkOperationFailure(
                    identifier,
                    BulkOperationFailureKind.NonRetryable,
                    "Session ID is required for DLQ resubmission on a session-enabled entity");
            }

            foreach (var sessionGroup in requestedMessages
                         .Where(message => !string.IsNullOrWhiteSpace(message.SessionId))
                         .GroupBy(message => message.SessionId!, StringComparer.Ordinal))
            {
                try
                {
                    await using var receiver = await AcceptSessionReceiverAsync(
                        client,
                        entityName,
                        subscription,
                        sessionGroup.Key,
                        deadLetter: true,
                        ct);
                    await ProcessSelectedMessagesAsync(
                        receiver,
                        sessionGroup.ToList(),
                        BulkOperationType.ResubmitDeadLetter,
                        requestedMessages.Count,
                        "Resubmitted",
                        ResubmitAsync,
                        succeeded,
                        failureExceptions,
                        progress,
                        ct);
                }
                catch (ServiceBusException ex)
                {
                    foreach (var identifier in sessionGroup)
                    {
                        failureExceptions[identifier] = ex;
                    }
                }
            }
        }
        else
        {
            var receiverOptions = new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = SubQueue.DeadLetter
            };

            await using var receiver = subscription != null
                ? client.CreateReceiver(entityName, subscription, receiverOptions)
                : client.CreateReceiver(entityName, receiverOptions);
            await ProcessSelectedMessagesAsync(
                receiver,
                requestedMessages,
                BulkOperationType.ResubmitDeadLetter,
                requestedMessages.Count,
                "Resubmitted",
                ResubmitAsync,
                succeeded,
                failureExceptions,
                progress,
                ct);
        }

        var failed = requestedMessages.Where(identifier => !succeeded.Contains(identifier)).ToList();
        var failures = failed
            .Select(identifier => explicitFailures.TryGetValue(identifier, out var explicitFailure)
                ? explicitFailure
                : failureExceptions.TryGetValue(identifier, out var ex)
                    ? CreateFailure(identifier, ex)
                    : new BulkOperationFailure(identifier, BulkOperationFailureKind.Retryable, "Message was not received from the dead-letter queue before the operation finished"))
            .ToList();

        return new BulkOperationExecutionResult(
            BulkOperationType.ResubmitDeadLetter,
            messageList.Count,
            succeeded.Count,
            failed,
            $"Resubmitted {succeeded.Count} of {messageList.Count} message(s)",
            CanResume: failures.Any(f => f.Kind == BulkOperationFailureKind.Retryable),
            Failures: failures,
            CompletionStatus: ct.IsCancellationRequested ? BulkOperationCompletionStatus.Cancelled : null);
    }

    private static async Task ProcessSelectedMessagesAsync(
        ServiceBusReceiver receiver,
        IReadOnlyCollection<MessageIdentifier> requestedMessages,
        BulkOperationType operationType,
        int totalRequestedCount,
        string completedVerb,
        Func<ServiceBusReceiver, ServiceBusReceivedMessage, CancellationToken, Task> settleAsync,
        HashSet<MessageIdentifier> succeeded,
        Dictionary<MessageIdentifier, ServiceBusException> failureExceptions,
        IProgress<BulkOperationProgress>? progress,
        CancellationToken ct)
    {
        var remaining = requestedMessages.ToHashSet();
        var heldMessages = new List<ServiceBusReceivedMessage>();
        var consecutiveEmptyBatches = 0;
        var scannedMessages = 0;

        async Task ReleaseHeldMessagesAsync()
        {
            foreach (var heldMessage in heldMessages)
            {
                try
                {
                    await receiver.AbandonMessageAsync(heldMessage, cancellationToken: CancellationToken.None);
                }
                catch (ServiceBusException ex)
                {
                    Log.Debug(ex, "Failed to release held message {SequenceNumber}", heldMessage.SequenceNumber);
                }
            }

            heldMessages.Clear();
        }

        try
        {
            while (remaining.Count > 0 &&
                   consecutiveEmptyBatches < MaxEmptyBatches &&
                   scannedMessages < MaxSelectedMessageScanCount &&
                   !ct.IsCancellationRequested)
            {
                var receivedMessages = await receiver.ReceiveMessagesAsync(DeleteBatchSize, DeleteReceiveTimeout, ct);
                if (receivedMessages.Count == 0)
                {
                    consecutiveEmptyBatches++;
                    continue;
                }

                consecutiveEmptyBatches = 0;
                scannedMessages += receivedMessages.Count;
                foreach (var receivedMessage in receivedMessages)
                {
                    MessageIdentifier? matchedIdentifier = null;
                    foreach (var identifier in remaining)
                    {
                        if (identifier.SequenceNumber == receivedMessage.SequenceNumber &&
                            (string.IsNullOrWhiteSpace(identifier.SessionId) ||
                             string.Equals(identifier.SessionId, receivedMessage.SessionId, StringComparison.Ordinal)))
                        {
                            matchedIdentifier = identifier;
                            break;
                        }
                    }

                    if (matchedIdentifier == null)
                    {
                        // Keep non-target messages locked until scan completes. Immediate abandon
                        // can redeliver same head messages and starve selected messages behind them.
                        heldMessages.Add(receivedMessage);
                        continue;
                    }

                    var identifierToSettle = matchedIdentifier.Value;
                    remaining.Remove(identifierToSettle);
                    try
                    {
                        await settleAsync(receiver, receivedMessage, ct);
                        succeeded.Add(identifierToSettle);
                    }
                    catch (ServiceBusException ex)
                    {
                        failureExceptions[identifierToSettle] = ex;
                        heldMessages.Add(receivedMessage);
                        Log.Debug(ex, "Failed to settle selected message {SequenceNumber}", receivedMessage.SequenceNumber);
                    }

                    var processedCount = succeeded.Count + failureExceptions.Count;
                    progress?.Report(new BulkOperationProgress(
                        operationType,
                        processedCount,
                        totalRequestedCount,
                        $"{completedVerb} {processedCount} of {totalRequestedCount} message(s)"));
                }

                if (heldMessages.Count >= DeleteBatchSize)
                {
                    await ReleaseHeldMessagesAsync();
                }
            }
        }
        finally
        {
            await ReleaseHeldMessagesAsync();
        }
    }

    private static ServiceBusMessage CreateResubmittedMessage(ServiceBusReceivedMessage deadLetterMessage)
    {
        var message = new ServiceBusMessage(deadLetterMessage.Body)
        {
            ContentType = deadLetterMessage.ContentType,
            CorrelationId = deadLetterMessage.CorrelationId,
            Subject = deadLetterMessage.Subject,
            To = deadLetterMessage.To,
            ReplyTo = deadLetterMessage.ReplyTo,
            ReplyToSessionId = deadLetterMessage.ReplyToSessionId,
            SessionId = deadLetterMessage.SessionId
        };

        if (deadLetterMessage.PartitionKey != null)
        {
            message.PartitionKey = deadLetterMessage.PartitionKey;
        }

        foreach (var property in deadLetterMessage.ApplicationProperties)
        {
            message.ApplicationProperties[property.Key] = property.Value;
        }

        return message;
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
        _ = await PurgeMessagesDetailedAsync(client, entityName, subscription, deadLetter, ct: ct);
    }

    public static async Task<BulkOperationExecutionResult> PurgeMessagesDetailedAsync(
        ServiceBusClient client,
        string entityName,
        string? subscription,
        bool deadLetter,
        IProgress<BulkOperationProgress>? progress = null,
        CancellationToken ct = default)
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
            progress?.Report(new BulkOperationProgress(
                BulkOperationType.Purge,
                deletedCount,
                0,
                $"Purged {deletedCount} message(s)"));
        }

        return new BulkOperationExecutionResult(
            BulkOperationType.Purge,
            deletedCount,
            deletedCount,
            [],
            deletedCount == 0 ? "No messages found to purge" : $"Purged {deletedCount} message(s)",
            CompletionStatus: ct.IsCancellationRequested ? BulkOperationCompletionStatus.Cancelled : null);
    }

    private static BulkOperationFailure CreateFailure(MessageIdentifier identifier, ServiceBusException ex)
    {
        var kind = ex.IsTransient
            ? BulkOperationFailureKind.Retryable
            : BulkOperationFailureKind.NonRetryable;

        return new BulkOperationFailure(identifier, kind, ex.Reason.ToString());
    }
}
