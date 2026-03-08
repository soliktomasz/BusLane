namespace BusLane.Models;

/// <summary>
/// Stores the last reviewed snapshot for a namespace inbox entity.
/// </summary>
public record NamespaceInboxReviewState(
    string NamespaceId,
    string EntityName,
    DateTimeOffset ReviewedAt,
    long ActiveMessageCount,
    long DeadLetterCount,
    long ScheduledCount,
    int ActiveAlertCount
);
