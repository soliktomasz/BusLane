namespace BusLane.Models;

using BusLane.Models.Entities;

/// <summary>
/// Represents an Azure Service Bus queue with its properties and metrics.
/// </summary>
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
) : IMessageEntity;
