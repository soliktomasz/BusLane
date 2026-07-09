namespace BusLane.Models;

/// <summary>
/// Local index entry for messages scheduled through BusLane.
/// </summary>
public record ScheduledMessageIndexEntry(
    string EntityName,
    string? SubscriptionName,
    long SequenceNumber,
    DateTimeOffset ScheduledEnqueueTime,
    string? MessageId,
    string BodyPreview,
    DateTimeOffset CreatedAt);
