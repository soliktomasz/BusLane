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
)
{
    // Pre-computed truncated body preview for efficient list rendering
    // Limit to ~200 chars to avoid performance issues with large message bodies
    private const int MaxPreviewLength = 200;

    public string BodyPreview { get; } = string.IsNullOrEmpty(Body)
        ? string.Empty
        : Body.Length <= MaxPreviewLength
            ? Body.ReplaceLineEndings(" ")
            : Body[..MaxPreviewLength].ReplaceLineEndings(" ") + "…";
}
