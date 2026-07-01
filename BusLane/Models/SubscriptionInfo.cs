namespace BusLane.Models;

using BusLane.Models.Entities;

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
    bool RequiresSession,
    TimeSpan? LockDuration = null,
    int? MaxDeliveryCount = null,
    TimeSpan? DefaultMessageTimeToLive = null,
    TimeSpan? AutoDeleteOnIdle = null,
    string? ForwardTo = null,
    string? ForwardDeadLetteredMessagesTo = null,
    bool? EnableBatchedOperations = null,
    bool? DeadLetteringOnMessageExpiration = null,
    string? Status = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null,
    long TransferMessageCount = 0,
    long TransferDeadLetterMessageCount = 0
) : IMessageEntity;
