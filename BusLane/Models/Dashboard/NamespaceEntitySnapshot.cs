namespace BusLane.Models.Dashboard;

using BusLane.Models;

/// <summary>
/// Carries the latest queue and subscription snapshot for a namespace refresh.
/// </summary>
public enum DashboardRefreshSection
{
    Queues,
    Topics,
    Subscriptions
}

public sealed record DashboardRefreshFailure(
    DashboardRefreshSection Section,
    string Message,
    DateTimeOffset Timestamp);

public record NamespaceEntitySnapshot(
    IReadOnlyList<QueueInfo> Queues,
    IReadOnlyList<SubscriptionInfo> Subscriptions,
    DateTimeOffset Timestamp,
    IReadOnlyList<DashboardRefreshSection> RefreshedSections)
{
    public NamespaceEntitySnapshot(
        IReadOnlyList<QueueInfo> queues,
        IReadOnlyList<SubscriptionInfo> subscriptions,
        DateTimeOffset timestamp)
        : this(queues, subscriptions, timestamp, [])
    {
    }
}
