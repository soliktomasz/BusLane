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
    IDictionary<string, object> Properties
);
