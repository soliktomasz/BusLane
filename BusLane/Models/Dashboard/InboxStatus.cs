namespace BusLane.Models.Dashboard;

/// <summary>
/// Triage health of a namespace inbox entity, used to drive the status dot and pill.
/// </summary>
public enum InboxStatus
{
    /// <summary>A healthy entity with no dead letters or scheduled backlog.</summary>
    Healthy,

    /// <summary>Entity carrying a scheduled-message backlog worth attention.</summary>
    Warning,

    /// <summary>Entity with a dead-letter accumulation that needs triage.</summary>
    Critical
}
