namespace BusLane.Services.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BusLane.Models;
using Serilog;

/// <summary>
/// Message, rule and health operations shared by connection-string and Azure-credential connections.
/// Subclasses supply the clients and the entity listing, which differ per authentication mode.
/// </summary>
public abstract class ServiceBusOperationsBase
{
    public abstract ServiceBusClient GetClient();

    protected abstract ServiceBusAdministrationClient AdminClient { get; }

    /// <summary>Hint shown when the connection health check fails authentication.</summary>
    protected abstract string ReauthenticateHint { get; }

    /// <summary>Cheapest call that proves the namespace is reachable with the current credentials.</summary>
    protected abstract Task ProbeAsync(CancellationToken ct);

    /// <summary>Maps an entity name as listed by this connection to the data-plane path.</summary>
    protected virtual string NormalizeEntityName(string entityName) => entityName;

    public abstract Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken ct = default);

    public abstract Task<IEnumerable<SubscriptionInfo>> GetSubscriptionsAsync(string topicName, CancellationToken ct = default);

    public async Task AbandonMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default) =>
        await ServiceBusOperations.AbandonMessageAsync(GetClient(), message, ct);

    public async Task CancelScheduledMessageAsync(string entityName, long sequenceNumber, CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        await using var sender = GetClient().CreateSender(entityName);
        await sender.CancelScheduledMessageAsync(sequenceNumber, ct);
    }

    public async Task CompleteMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default) =>
        await ServiceBusOperations.CompleteMessageAsync(GetClient(), message, ct);

    public async Task CreateQueueAsync(QueueCreationOptions options, CancellationToken ct = default)
    {
        var sdkOptions = ServiceBusOperations.BuildCreateQueueOptions(options);
        await AdminClient.CreateQueueAsync(sdkOptions, ct);
    }

    public async Task CreateSubscriptionRuleAsync(
        string topicName,
        string subscriptionName,
        SubscriptionRuleCreationOptions options,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        topicName = NormalizeEntityName(topicName);
        ArgumentNullException.ThrowIfNull(options);

        var sdkOptions = ServiceBusOperations.BuildCreateRuleOptions(options);
        await AdminClient.CreateRuleAsync(topicName, subscriptionName, sdkOptions, ct);
    }

    public async Task CreateTopicAsync(TopicCreationOptions options, CancellationToken ct = default)
    {
        var sdkOptions = ServiceBusOperations.BuildCreateTopicOptions(options);
        await AdminClient.CreateTopicAsync(sdkOptions, ct);
    }

    public async Task DeadLetterMessageAsync(ReceivedMessageInfo message, string? reason = null, string? description = null, CancellationToken ct = default) =>
        await ServiceBusOperations.DeadLetterMessageAsync(GetClient(), message, reason, description, ct);

    public async Task DeferMessageAsync(ReceivedMessageInfo message, CancellationToken ct = default) =>
        await ServiceBusOperations.DeferMessageAsync(GetClient(), message, ct);

    public async Task<BulkOperationExecutionResult> DeleteMessagesDetailedAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageIdentifier> messages,
        bool deadLetter = false,
        IProgress<BulkOperationProgress>? progress = null,
        bool requiresSession = false,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        return await ServiceBusOperations.DeleteMessagesDetailedAsync(GetClient(), entityName, subscription, messages, deadLetter, progress, requiresSession, ct);
    }

    public async Task DeleteSubscriptionRuleAsync(
        string topicName,
        string subscriptionName,
        string ruleName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        topicName = NormalizeEntityName(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);

        await AdminClient.DeleteRuleAsync(topicName, subscriptionName, ruleName, ct);
    }

    public async Task<IReadOnlyList<SessionInspectorItem>> GetSessionInspectorItemsAsync(
        string entityName,
        string? subscription,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        return await ServiceBusOperations.GetSessionInspectorItemsAsync(GetClient(), entityName, subscription, ct);
    }

    public async Task<IReadOnlyList<SubscriptionRuleInfo>> GetSubscriptionRulesAsync(
        string topicName,
        string subscriptionName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
        topicName = NormalizeEntityName(topicName);

        var rules = new List<SubscriptionRuleInfo>();
        await foreach (var rule in AdminClient.GetRulesAsync(topicName, subscriptionName, ct))
        {
            rules.Add(ServiceBusOperations.MapToSubscriptionRuleInfo(rule));
        }

        return rules;
    }

    public async Task<IEnumerable<MessageInfo>> PeekMessagesAsync(
        string entityName, string? subscription, int count, long? fromSequenceNumber, bool deadLetter,
        bool requiresSession = false, string? sessionId = null, CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);

        var messages = requiresSession && !string.IsNullOrWhiteSpace(sessionId)
            ? await ServiceBusOperations.PeekSessionMessagesAsync(GetClient(), entityName, subscription, sessionId, count, fromSequenceNumber, deadLetter, ct)
            : requiresSession
                ? await ServiceBusOperations.PeekSessionMessagesAsync(GetClient(), entityName, subscription, count, fromSequenceNumber, deadLetter, ct)
            : await ServiceBusOperations.PeekStandardMessagesAsync(GetClient(), entityName, subscription, count, fromSequenceNumber, deadLetter, ct);

        return messages.Select(ServiceBusOperations.MapToMessageInfo);
    }

    public async Task<BulkOperationExecutionResult> PurgeMessagesDetailedAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        IProgress<BulkOperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        return await ServiceBusOperations.PurgeMessagesDetailedAsync(GetClient(), entityName, subscription, deadLetter, progress, ct);
    }

    public async Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveDeferredMessagesAsync(
        string entityName,
        string? subscription,
        IEnumerable<long> sequenceNumbers,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        return await ServiceBusOperations.ReceiveDeferredMessagesAsync(
            GetClient(), entityName, subscription, sequenceNumbers, requiresSession, sessionId, ct);
    }

    public async Task<IReadOnlyList<ReceivedMessageInfo>> ReceiveMessagesAsync(
        string entityName,
        string? subscription,
        int count,
        bool deadLetter,
        bool requiresSession = false,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        return await ServiceBusOperations.ReceiveMessagesAsync(
            GetClient(), entityName, subscription, count, deadLetter, requiresSession, sessionId, ct);
    }

    public async Task<DateTimeOffset> RenewMessageLockAsync(ReceivedMessageInfo message, CancellationToken ct = default) =>
        await ServiceBusOperations.RenewMessageLockAsync(GetClient(), message, ct);

    public async Task<BulkOperationExecutionResult> ResendMessagesDetailedAsync(
        string entityName,
        IEnumerable<MessageInfo> messages,
        IProgress<BulkOperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        return await ServiceBusOperations.ResendMessagesDetailedAsync(GetClient(), entityName, messages, progress, ct);
    }

    public async Task<BulkOperationExecutionResult> ResubmitDeadLetterMessagesDetailedAsync(
        string entityName,
        string? subscription,
        IEnumerable<MessageInfo> messages,
        IProgress<BulkOperationProgress>? progress = null,
        bool requiresSession = false,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        return await ServiceBusOperations.ResubmitDeadLetterMessagesDetailedAsync(GetClient(), entityName, subscription, messages, progress, requiresSession, ct);
    }

    public async Task<long> ScheduleMessageAsync(
        string entityName,
        string body,
        IDictionary<string, object>? properties,
        DateTimeOffset scheduledEnqueueTime,
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
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        await using var sender = GetClient().CreateSender(entityName);
        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, null, properties);
        return await sender.ScheduleMessageAsync(msg, scheduledEnqueueTime, ct);
    }

    public async Task SendMessageAsync(
        string entityName, string body, IDictionary<string, object>? properties,
        string? contentType = null, string? correlationId = null, string? messageId = null,
        string? sessionId = null, string? subject = null, string? to = null,
        string? replyTo = null, string? replyToSessionId = null, string? partitionKey = null,
        TimeSpan? timeToLive = null, DateTimeOffset? scheduledEnqueueTime = null,
        CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        Log.Debug("Sending message to {EntityName}", entityName);
        await using var sender = GetClient().CreateSender(entityName);

        var msg = new ServiceBusMessage(body);
        ServiceBusOperations.ApplyMessageProperties(msg, contentType, correlationId, messageId, sessionId,
            subject, to, replyTo, replyToSessionId, partitionKey, timeToLive, scheduledEnqueueTime, properties);

        await sender.SendMessageAsync(msg, ct);
        Log.Information("Message sent to {EntityName} (MessageId: {MessageId})", entityName, msg.MessageId);
    }

    public async Task PurgeMessagesAsync(string entityName, string? subscription, bool deadLetter, CancellationToken ct = default)
    {
        entityName = NormalizeEntityName(entityName);
        Log.Information("Purging messages from {EntityName}{Subscription} (DeadLetter: {DeadLetter})",
            entityName, subscription != null ? $"/{subscription}" : "", deadLetter);
        await ServiceBusOperations.PurgeMessagesAsync(GetClient(), entityName, subscription, deadLetter, ct);
        Log.Information("Purge completed for {EntityName}", entityName);
    }

    public async Task<BulkOperationPreview> PreviewPurgeMessagesAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct = default)
    {
        var (count, requiresSession) = await GetEstimatedCountAsync(entityName, subscription, deadLetter, ct);
        return BuildPurgePreview(entityName, subscription, deadLetter, count, requiresSession);
    }

    public async Task<ConnectionHealthReport> CheckConnectionHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            await ProbeAsync(timeoutCts.Token);

            return new ConnectionHealthReport(ConnectionHealthState.Healthy, "Connection healthy");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ConnectionHealthReport(ConnectionHealthState.AuthExpired, ex.Message, ReauthenticateHint);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceBusy)
        {
            return new ConnectionHealthReport(ConnectionHealthState.Throttled, ex.Message, "Retry later or reduce refresh frequency.");
        }
        catch (ServiceBusException ex) when (
            ex.Reason == ServiceBusFailureReason.ServiceCommunicationProblem ||
            ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            return new ConnectionHealthReport(ConnectionHealthState.NetworkFailed, ex.Message, "Check connectivity to the namespace endpoint.");
        }
        catch (Exception ex)
        {
            return new ConnectionHealthReport(ConnectionHealthState.Degraded, ex.Message, "Review diagnostics and retry the connection.");
        }
    }

    private async Task<(int Count, bool RequiresSession)> GetEstimatedCountAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(subscription))
        {
            var subscriptions = await GetSubscriptionsAsync(entityName, ct);
            var match = subscriptions.FirstOrDefault(s => s.Name == subscription);
            if (match != null)
            {
                return ((int)(deadLetter ? match.DeadLetterCount : match.ActiveMessageCount), match.RequiresSession);
            }
        }

        var queue = await GetQueueInfoAsync(entityName, ct);
        if (queue != null)
        {
            return ((int)(deadLetter ? queue.DeadLetterCount : queue.ActiveMessageCount), queue.RequiresSession);
        }

        return (0, false);
    }

    private static BulkOperationPreview BuildPurgePreview(
        string entityName,
        string? subscription,
        bool deadLetter,
        int estimatedCount,
        bool requiresSession)
    {
        var scope = subscription != null
            ? $"{entityName}/{subscription}{(deadLetter ? " (DLQ)" : string.Empty)}"
            : $"{entityName}{(deadLetter ? " (DLQ)" : string.Empty)}";

        var warnings = new List<string>();
        if (requiresSession)
        {
            warnings.Add("Session-enabled entities may require multiple receive cycles to drain fully.");
        }

        if (estimatedCount == 0)
        {
            warnings.Add("No currently known messages matched this purge scope.");
        }

        return new BulkOperationPreview(
            BulkOperationType.Purge,
            scope,
            estimatedCount,
            [],
            warnings,
            requiresSession,
            new BulkOperationScope(entityName, subscription, deadLetter, requiresSession, [], new Dictionary<string, string>()),
            IsHighRisk: true);
    }
}
