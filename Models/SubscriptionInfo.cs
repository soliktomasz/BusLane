using BusLane.Models.Entities;

namespace BusLane.Models;

/// <summary>
/// Represents an Azure Service Bus topic subscription with its properties and metrics.
/// </summary>
public record SubscriptionInfo(
    string Name,
    string TopicName,
    long MessageCount,
    long ActiveMessageCount,
    long DeadLetterCount,
    DateTimeOffset? AccessedAt,
    bool RequiresSession
) : IMessageEntity;
