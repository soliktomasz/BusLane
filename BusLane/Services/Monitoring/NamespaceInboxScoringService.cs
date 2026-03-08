namespace BusLane.Services.Monitoring;

using BusLane.Models;
using BusLane.Models.Dashboard;

/// <summary>
/// Calculates a simple explainable priority score for namespace inbox entities.
/// </summary>
public class NamespaceInboxScoringService : INamespaceInboxScoringService
{
    private static readonly TimeSpan DefaultComparisonWindow = TimeSpan.FromMinutes(15);

    private readonly IMetricsHistoryStore _metricsHistoryStore;

    public NamespaceInboxScoringService(IMetricsHistoryStore metricsHistoryStore)
    {
        _metricsHistoryStore = metricsHistoryStore;
    }

    public IReadOnlyList<NamespaceInboxItem> Rank(
        IEnumerable<QueueInfo> queues,
        IEnumerable<SubscriptionInfo> subscriptions,
        IEnumerable<AlertEvent> activeAlerts,
        TimeSpan? comparisonWindow = null)
    {
        var window = comparisonWindow ?? DefaultComparisonWindow;
        var unacknowledgedAlerts = activeAlerts
            .Where(alert => !alert.IsAcknowledged)
            .GroupBy(alert => alert.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var items = new List<NamespaceInboxItem>();

        foreach (var queue in queues)
        {
            items.Add(CreateQueueItem(queue, unacknowledgedAlerts, window));
        }

        foreach (var subscription in subscriptions)
        {
            items.Add(CreateSubscriptionItem(subscription, unacknowledgedAlerts, window));
        }

        return items
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private NamespaceInboxItem CreateQueueItem(
        QueueInfo queue,
        IReadOnlyDictionary<string, int> unacknowledgedAlerts,
        TimeSpan window)
    {
        var reasons = new List<string>();
        var deadLetterGrowth = GetPositiveDelta(queue.Name, "DeadLetterCount", window);
        var activeGrowth = GetPositiveDelta(queue.Name, "ActiveMessageCount", window);
        var activeAlertCount = unacknowledgedAlerts.GetValueOrDefault(queue.Name, 0);

        double score = queue.DeadLetterCount * 3;

        if (deadLetterGrowth > 0)
        {
            score += deadLetterGrowth * 4;
            reasons.Add($"Dead-letter count up by {deadLetterGrowth:0.#}");
        }

        if (activeGrowth > 0)
        {
            score += activeGrowth * 1.5;
            reasons.Add($"Backlog up by {activeGrowth:0.#}");
        }

        if (queue.ScheduledCount > 0)
        {
            score += queue.ScheduledCount * 0.5;
            reasons.Add($"Scheduled backlog at {queue.ScheduledCount}");
        }

        if (activeAlertCount > 0)
        {
            score += activeAlertCount * 25;
            reasons.Add($"{activeAlertCount} active alerts");
        }

        if (reasons.Count == 0 && queue.DeadLetterCount > 0)
        {
            reasons.Add($"Dead-letter count at {queue.DeadLetterCount}");
        }

        return new NamespaceInboxItem(
            queue.Name,
            EntityType.Queue,
            TopicName: null,
            queue.RequiresSession,
            queue.ActiveMessageCount,
            queue.DeadLetterCount,
            queue.ScheduledCount,
            activeAlertCount,
            score,
            reasons);
    }

    private NamespaceInboxItem CreateSubscriptionItem(
        SubscriptionInfo subscription,
        IReadOnlyDictionary<string, int> unacknowledgedAlerts,
        TimeSpan window)
    {
        var entityName = $"{subscription.TopicName}/{subscription.Name}";
        var reasons = new List<string>();
        var deadLetterGrowth = GetPositiveDelta(entityName, "DeadLetterCount", window);
        var activeGrowth = GetPositiveDelta(entityName, "ActiveMessageCount", window);
        var activeAlertCount = unacknowledgedAlerts.GetValueOrDefault(entityName, 0);

        double score = subscription.DeadLetterCount * 3;

        if (deadLetterGrowth > 0)
        {
            score += deadLetterGrowth * 4;
            reasons.Add($"Dead-letter count up by {deadLetterGrowth:0.#}");
        }

        if (activeGrowth > 0)
        {
            score += activeGrowth * 1.5;
            reasons.Add($"Backlog up by {activeGrowth:0.#}");
        }

        if (activeAlertCount > 0)
        {
            score += activeAlertCount * 25;
            reasons.Add($"{activeAlertCount} active alerts");
        }

        if (reasons.Count == 0 && subscription.DeadLetterCount > 0)
        {
            reasons.Add($"Dead-letter count at {subscription.DeadLetterCount}");
        }

        return new NamespaceInboxItem(
            entityName,
            EntityType.Subscription,
            subscription.TopicName,
            subscription.RequiresSession,
            subscription.ActiveMessageCount,
            subscription.DeadLetterCount,
            ScheduledCount: 0,
            activeAlertCount,
            score,
            reasons);
    }

    private double GetPositiveDelta(string entityName, string metricName, TimeSpan window)
    {
        var comparison = _metricsHistoryStore.CompareWindows(entityName, metricName, window);
        return Math.Max(0, comparison.Delta);
    }
}
