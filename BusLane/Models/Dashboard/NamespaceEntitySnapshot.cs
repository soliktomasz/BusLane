namespace BusLane.Models.Dashboard;

using BusLane.Models;

/// <summary>
/// Carries the latest queue and subscription snapshot for a namespace refresh.
/// </summary>
public record NamespaceEntitySnapshot(
    IReadOnlyList<QueueInfo> Queues,
    IReadOnlyList<SubscriptionInfo> Subscriptions,
    DateTimeOffset Timestamp
);
