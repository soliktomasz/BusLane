namespace BusLane.Models;

public sealed record ReplayDestination(
    string NamespaceName,
    ConnectionEnvironment Environment,
    string EntityName,
    string EntityType,
    bool RequiresSession,
    ScheduledMessageConnectionContext? ScheduledConnectionContext = null);

public sealed record ReplayRequest
{
    public required CorrelationMessage Source { get; init; }
    public required ReplayDestination Destination { get; init; }
    public required string Body { get; init; }
    public string? ContentType { get; init; }
    public string? CorrelationId { get; init; }
    public string? MessageId { get; init; }
    public string? SessionId { get; init; }
    public string? Subject { get; init; }
    public string? To { get; init; }
    public string? ReplyTo { get; init; }
    public string? ReplyToSessionId { get; init; }
    public string? PartitionKey { get; init; }
    public TimeSpan? TimeToLive { get; init; }
    public IReadOnlyDictionary<string, object> Properties { get; init; } =
        new Dictionary<string, object>();
    public DateTimeOffset? ScheduledEnqueueTime { get; init; }
    public int RateLimitPerSecond { get; init; } = 1;
    public bool IsConfirmed { get; init; }
    public bool IsProductionAcknowledged { get; init; }
}

public sealed record ReplayFieldChange(string Field, string? SourceValue, string? ReplayValue);

public sealed record ReplayPreview(
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ReplayFieldChange> Changes)
{
    public bool IsValid => ValidationErrors.Count == 0;
}

public sealed record ReplayResult(
    bool IsSuccess,
    bool IsScheduled,
    string Message,
    long? ScheduledSequenceNumber = null,
    IReadOnlyList<string>? ValidationErrors = null,
    string? AuditWarning = null);

public enum ReplayAuditOutcome
{
    ValidationFailed,
    Cancelled,
    Attempted,
    Succeeded,
    Failed
}

public sealed record ReplayAuditEntry(
    string Id,
    DateTimeOffset Timestamp,
    ReplayAuditOutcome Outcome,
    string SourceMessageId,
    string? CorrelationId,
    string DestinationNamespace,
    ConnectionEnvironment DestinationEnvironment,
    string DestinationEntity,
    bool IsScheduled,
    int RateLimitPerSecond,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyList<string> ValidationMessages,
    string ResultMessage);
