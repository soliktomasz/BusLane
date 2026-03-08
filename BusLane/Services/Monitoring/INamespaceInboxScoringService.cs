namespace BusLane.Services.Monitoring;

using BusLane.Models;

/// <summary>
/// Produces ranked namespace inbox items from current entity and alert state.
/// </summary>
public interface INamespaceInboxScoringService
{
    IReadOnlyList<NamespaceInboxItem> Rank(
        IEnumerable<QueueInfo> queues,
        IEnumerable<SubscriptionInfo> subscriptions,
        IEnumerable<AlertEvent> activeAlerts,
        TimeSpan? comparisonWindow = null);
}
