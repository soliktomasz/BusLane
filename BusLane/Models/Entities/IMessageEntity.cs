namespace BusLane.Models.Entities;
/// <summary>
/// Base interface for message-containing entities (queues, subscriptions).
/// Provides common properties shared across entity types.
/// </summary>
public interface IMessageEntity
{
    string Name { get; }
    long MessageCount { get; }
    long ActiveMessageCount { get; }
    long DeadLetterCount { get; }
    DateTimeOffset? AccessedAt { get; }
    bool RequiresSession { get; }
}
