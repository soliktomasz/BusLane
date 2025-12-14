namespace BusLane.Models;

public record SubscriptionInfo(
    string Name,
    string TopicName,
    long MessageCount,
    long ActiveMessageCount,
    long DeadLetterCount,
    DateTimeOffset? AccessedAt,
    bool RequiresSession
);
