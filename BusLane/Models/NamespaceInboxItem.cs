namespace BusLane.Models;

using BusLane.Models.Dashboard;

/// <summary>
/// Represents a ranked entity in the namespace triage inbox.
/// </summary>
public record NamespaceInboxItem(
    string EntityName,
    EntityType EntityType,
    string? TopicName,
    bool RequiresSession,
    long ActiveMessageCount,
    long DeadLetterCount,
    long ScheduledCount,
    int ActiveAlertCount,
    double Score,
    IReadOnlyList<string> Reasons
);
