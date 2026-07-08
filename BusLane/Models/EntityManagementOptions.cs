namespace BusLane.Models;

/// <summary>
/// Filter kinds supported when displaying or creating subscription rules.
/// </summary>
public enum SubscriptionRuleFilterType
{
    True,
    Sql,
    Correlation
}

/// <summary>
/// Optional queue properties to update.
/// </summary>
public record QueueUpdateOptions(
    TimeSpan? LockDuration = null,
    TimeSpan? DefaultMessageTimeToLive = null,
    bool? DeadLetteringOnMessageExpiration = null,
    bool? EnableBatchedOperations = null);

/// <summary>
/// Optional topic properties to update.
/// </summary>
public record TopicUpdateOptions(
    TimeSpan? DefaultMessageTimeToLive = null,
    bool? EnableBatchedOperations = null);

/// <summary>
/// Optional subscription properties to update.
/// </summary>
public record SubscriptionUpdateOptions(
    TimeSpan? LockDuration = null,
    int? MaxDeliveryCount = null,
    TimeSpan? DefaultMessageTimeToLive = null,
    TimeSpan? AutoDeleteOnIdle = null,
    string? ForwardTo = null,
    string? ForwardDeadLetteredMessagesTo = null,
    bool? EnableBatchedOperations = null,
    bool? DeadLetteringOnMessageExpiration = null);

/// <summary>
/// Display information for a Service Bus subscription rule.
/// </summary>
public record SubscriptionRuleInfo(
    string Name,
    SubscriptionRuleFilterType FilterType,
    string DisplayExpression,
    string? SqlExpression = null,
    string? ActionExpression = null,
    IReadOnlyDictionary<string, string>? CorrelationProperties = null);

/// <summary>
/// Options for creating a Service Bus subscription rule.
/// </summary>
public record SubscriptionRuleCreationOptions(
    string Name,
    string? SqlExpression = null,
    SubscriptionRuleFilterType FilterType = SubscriptionRuleFilterType.Sql,
    string? ActionExpression = null,
    IReadOnlyDictionary<string, string>? CorrelationProperties = null);
