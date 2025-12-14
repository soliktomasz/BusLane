namespace BusLane.Models;

public record QueueInfo(
    string Name,
    long MessageCount,
    long ActiveMessageCount,
    long DeadLetterCount,
    long ScheduledCount,
    long SizeInBytes,
    DateTimeOffset? AccessedAt,
    bool RequiresSession,
    TimeSpan DefaultMessageTtl,
    TimeSpan LockDuration
);
