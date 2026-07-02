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
    string? DeadLetterErrorDescription = null,
    bool IsBodyPreviewOnly = false
)
{
    // Pre-computed truncated body preview for efficient list rendering
    // Limit to ~200 chars to avoid performance issues with large message bodies
    public const int MaxPreviewLength = 200;

    public string BodyPreview { get; } = IsBodyPreviewOnly ? Body : CreateBodyPreview(Body);

    public static string CreateBodyPreview(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        return body.Length <= MaxPreviewLength
            ? body.ReplaceLineEndings(" ")
            : body[..MaxPreviewLength].ReplaceLineEndings(" ") + "…";
    }

    public bool IsBodyXml
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Body)) return false;

            var trimmed = Body.Trim();
            return trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                   (trimmed.StartsWith("<") && trimmed.Contains("</", StringComparison.Ordinal) && trimmed.EndsWith(">"));
        }
    }
}
