namespace BusLane.Models;

public record TopicInfo(
    string Name,
    long SizeInBytes,
    int SubscriptionCount,
    DateTimeOffset? AccessedAt,
    TimeSpan DefaultMessageTtl
);
