namespace BusLane.Models;

/// <summary>
/// Portable, secret-free description of namespace entities and rules.
/// </summary>
public record NamespaceTopologyDocument(
    int SchemaVersion,
    DateTimeOffset ExportedAt,
    IReadOnlyList<QueueTopology> Queues,
    IReadOnlyList<TopicTopology> Topics);

/// <summary>
/// Desired queue settings captured in a namespace topology document.
/// </summary>
public record QueueTopology(
    string Name,
    bool RequiresSession,
    TimeSpan DefaultMessageTimeToLive,
    TimeSpan LockDuration,
    bool? DeadLetteringOnMessageExpiration,
    bool? EnableBatchedOperations);

/// <summary>
/// Desired topic settings and subscriptions captured in a namespace topology document.
/// </summary>
public record TopicTopology(
    string Name,
    TimeSpan DefaultMessageTimeToLive,
    bool? EnableBatchedOperations,
    IReadOnlyList<SubscriptionTopology> Subscriptions);

/// <summary>
/// Desired subscription settings, forwarding paths, and rules captured in a namespace topology document.
/// </summary>
public record SubscriptionTopology(
    string Name,
    bool RequiresSession,
    TimeSpan? LockDuration,
    int? MaxDeliveryCount,
    TimeSpan? DefaultMessageTimeToLive,
    TimeSpan? AutoDeleteOnIdle,
    string? ForwardTo,
    string? ForwardDeadLetteredMessagesTo,
    bool? EnableBatchedOperations,
    bool? DeadLetteringOnMessageExpiration,
    IReadOnlyList<RuleTopology> Rules);

/// <summary>
/// Desired subscription rule filter and action captured in a namespace topology document.
/// </summary>
public record RuleTopology(
    string Name,
    SubscriptionRuleFilterType FilterType,
    string DisplayExpression,
    string? SqlExpression,
    string? ActionExpression,
    IReadOnlyDictionary<string, string>? CorrelationProperties);

public enum TopologyImportActionType
{
    CreateQueue,
    UpdateQueue,
    CreateTopic,
    UpdateTopic,
    CreateSubscription,
    UpdateSubscription,
    CreateRule,
    Skip
}

public record TopologyImportAction(
    TopologyImportActionType ActionType,
    string EntityPath,
    string Description);

public record TopologyImportPlan(
    IReadOnlyList<TopologyImportAction> Actions)
{
    public bool HasChanges => Actions.Any(a => a.ActionType != TopologyImportActionType.Skip);
}
