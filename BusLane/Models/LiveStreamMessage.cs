namespace BusLane.Models;

public record LiveStreamMessage(
    string MessageId,
    string? CorrelationId,
    string? ContentType,
    string Body,
    DateTimeOffset ReceivedAt,
    string EntityName,
    string EntityType, // "Queue" or "Subscription"
    string? TopicName, // Only for subscriptions
    long SequenceNumber,
    string? SessionId,
    IDictionary<string, object> Properties
);

