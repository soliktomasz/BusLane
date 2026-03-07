namespace BusLane.Models;

/// <summary>
/// Represents discovered session details for a session-enabled entity.
/// </summary>
public record SessionInspectorItem(
    string SessionId,
    long ActiveMessageCount,
    long DeadLetterMessageCount,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset? LockedUntil,
    string? State)
{
    public long TotalMessageCount => ActiveMessageCount + DeadLetterMessageCount;
    public bool HasDeadLetter => DeadLetterMessageCount > 0;
}
