namespace BusLane.Models;

public record MessageInfo(
    string MessageId,
    string? CorrelationId,
    string? ContentType,
    string Body,
    DateTimeOffset EnqueuedTime,
    DateTimeOffset? ScheduledEnqueueTime,
    long SequenceNumber,
    int DeliveryCount,
    string? SessionId,
    IDictionary<string, object> Properties,
    // Additional properties
    string? Subject = null,
    string? To = null,
    string? ReplyTo = null,
    string? ReplyToSessionId = null,
    string? PartitionKey = null,
    TimeSpan? TimeToLive = null,
    DateTimeOffset? ExpiresAt = null,
    string? LockToken = null,
    DateTimeOffset? LockedUntil = null,
    // Dead letter specific properties
    string? DeadLetterSource = null,
    string? DeadLetterReason = null,
    string? DeadLetterErrorDescription = null
);
