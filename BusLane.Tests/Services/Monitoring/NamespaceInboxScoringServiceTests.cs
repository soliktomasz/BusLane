namespace BusLane.Tests.Services.Monitoring;

using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.Monitoring;
using FluentAssertions;

public class NamespaceInboxScoringServiceTests
{
    private readonly FakeMetricsHistoryStore _metricsHistoryStore = new();

    [Fact]
    public void Rank_WhenDeadLetterGrowthIsHigher_RanksEntityFirst()
    {
        // Arrange
        _metricsHistoryStore.SetComparison("stable-queue", "DeadLetterCount", currentAverage: 10, previousAverage: 10);
        _metricsHistoryStore.SetComparison("rising-queue", "DeadLetterCount", currentAverage: 25, previousAverage: 5);
        _metricsHistoryStore.SetComparison("stable-queue", "ActiveMessageCount", currentAverage: 20, previousAverage: 20);
        _metricsHistoryStore.SetComparison("rising-queue", "ActiveMessageCount", currentAverage: 20, previousAverage: 20);

        var sut = new NamespaceInboxScoringService(_metricsHistoryStore);

        var queues = new[]
        {
            CreateQueueInfo("stable-queue", activeMessageCount: 20, deadLetterCount: 10),
            CreateQueueInfo("rising-queue", activeMessageCount: 20, deadLetterCount: 25)
        };

        // Act
        var rankedItems = sut.Rank(queues, [], []);

        // Assert
        rankedItems.Select(item => item.EntityName).Should().ContainInOrder("rising-queue", "stable-queue");
        rankedItems[0].Reasons.Should().Contain(reason => reason.Contains("Dead-letter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rank_WhenEntityHasUnacknowledgedAlerts_IncreasesScoreAndAddsReason()
    {
        // Arrange
        _metricsHistoryStore.SetComparison("alert-queue", "DeadLetterCount", currentAverage: 3, previousAverage: 3);
        _metricsHistoryStore.SetComparison("alert-queue", "ActiveMessageCount", currentAverage: 5, previousAverage: 5);

        var activeAlerts = new[]
        {
            CreateAlert("alert-queue", isAcknowledged: false),
            CreateAlert("alert-queue", isAcknowledged: false)
        };

        var sut = new NamespaceInboxScoringService(_metricsHistoryStore);

        // Act
        var rankedItem = sut.Rank([CreateQueueInfo("alert-queue", activeMessageCount: 5, deadLetterCount: 3)], [], activeAlerts).Single();

        // Assert
        rankedItem.ActiveAlertCount.Should().Be(2);
        rankedItem.Score.Should().BeGreaterThan(0);
        rankedItem.Reasons.Should().Contain(reason => reason.Contains("2 active alerts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rank_WhenQueueHasScheduledMessages_ScheduledPressureOnlyAffectsQueues()
    {
        // Arrange
        _metricsHistoryStore.SetComparison("queue-with-scheduled", "DeadLetterCount", currentAverage: 0, previousAverage: 0);
        _metricsHistoryStore.SetComparison("queue-with-scheduled", "ActiveMessageCount", currentAverage: 5, previousAverage: 5);
        _metricsHistoryStore.SetComparison("topic-a/sub-a", "DeadLetterCount", currentAverage: 0, previousAverage: 0);
        _metricsHistoryStore.SetComparison("topic-a/sub-a", "ActiveMessageCount", currentAverage: 5, previousAverage: 5);

        var sut = new NamespaceInboxScoringService(_metricsHistoryStore);

        var queues = new[]
        {
            CreateQueueInfo("queue-with-scheduled", activeMessageCount: 5, deadLetterCount: 0, scheduledCount: 100)
        };

        var subscriptions = new[]
        {
            CreateSubscriptionInfo("topic-a", "sub-a", activeMessageCount: 5, deadLetterCount: 0)
        };

        // Act
        var rankedItems = sut.Rank(queues, subscriptions, []);

        // Assert
        rankedItems[0].EntityName.Should().Be("queue-with-scheduled");
        rankedItems[0].Reasons.Should().Contain(reason => reason.Contains("Scheduled", StringComparison.OrdinalIgnoreCase));
        rankedItems.Single(item => item.EntityType == EntityType.Subscription).Reasons.Should().NotContain(reason => reason.Contains("Scheduled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rank_ReturnsItemsInDescendingScoreOrder()
    {
        // Arrange
        _metricsHistoryStore.SetComparison("low", "DeadLetterCount", currentAverage: 1, previousAverage: 1);
        _metricsHistoryStore.SetComparison("low", "ActiveMessageCount", currentAverage: 5, previousAverage: 5);
        _metricsHistoryStore.SetComparison("medium", "DeadLetterCount", currentAverage: 5, previousAverage: 1);
        _metricsHistoryStore.SetComparison("medium", "ActiveMessageCount", currentAverage: 10, previousAverage: 5);
        _metricsHistoryStore.SetComparison("high", "DeadLetterCount", currentAverage: 15, previousAverage: 3);
        _metricsHistoryStore.SetComparison("high", "ActiveMessageCount", currentAverage: 50, previousAverage: 10);

        var sut = new NamespaceInboxScoringService(_metricsHistoryStore);

        var queues = new[]
        {
            CreateQueueInfo("low", activeMessageCount: 5, deadLetterCount: 1),
            CreateQueueInfo("medium", activeMessageCount: 10, deadLetterCount: 5),
            CreateQueueInfo("high", activeMessageCount: 50, deadLetterCount: 15)
        };

        // Act
        var rankedItems = sut.Rank(queues, [], []);

        // Assert
        rankedItems.Select(item => item.Score).Should().BeInDescendingOrder();
        rankedItems.Select(item => item.EntityName).Should().ContainInOrder("high", "medium", "low");
    }

    private static QueueInfo CreateQueueInfo(
        string name,
        long activeMessageCount,
        long deadLetterCount,
        long scheduledCount = 0)
    {
        return new QueueInfo(
            name,
            MessageCount: activeMessageCount + deadLetterCount + scheduledCount,
            ActiveMessageCount: activeMessageCount,
            DeadLetterCount: deadLetterCount,
            ScheduledCount: scheduledCount,
            SizeInBytes: 0,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: false,
            DefaultMessageTtl: TimeSpan.FromDays(14),
            LockDuration: TimeSpan.FromMinutes(1));
    }

    private static SubscriptionInfo CreateSubscriptionInfo(
        string topicName,
        string subscriptionName,
        long activeMessageCount,
        long deadLetterCount)
    {
        return new SubscriptionInfo(
            subscriptionName,
            topicName,
            MessageCount: activeMessageCount + deadLetterCount,
            ActiveMessageCount: activeMessageCount,
            DeadLetterCount: deadLetterCount,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: false);
    }

    private static AlertEvent CreateAlert(string entityName, bool isAcknowledged)
    {
        return new AlertEvent(
            Guid.NewGuid().ToString(),
            new AlertRule(
                Guid.NewGuid().ToString(),
                "High dead letters",
                AlertType.DeadLetterThreshold,
                AlertSeverity.Warning,
                Threshold: 10),
            entityName,
            "Queue",
            CurrentValue: 25,
            TriggeredAt: DateTimeOffset.UtcNow,
            IsAcknowledged: isAcknowledged);
    }

    private sealed class FakeMetricsHistoryStore : IMetricsHistoryStore
    {
        private readonly Dictionary<(string EntityName, string MetricName), MetricWindowComparison> _comparisons = [];

        public void SetComparison(string entityName, string metricName, double currentAverage, double previousAverage)
        {
            _comparisons[(entityName, metricName)] = new MetricWindowComparison(
                entityName,
                metricName,
                TimeSpan.FromMinutes(15),
                currentAverage,
                previousAverage);
        }

        public void RecordSnapshots(IEnumerable<MetricSnapshot> snapshots)
        {
        }

        public IReadOnlyList<MetricSnapshot> GetHistory(string entityName, string metricName, TimeSpan duration)
        {
            return [];
        }

        public MetricWindowComparison CompareWindows(string entityName, string metricName, TimeSpan window)
        {
            return _comparisons.TryGetValue((entityName, metricName), out var comparison)
                ? comparison
                : new MetricWindowComparison(entityName, metricName, window, 0, 0);
        }

        public void CleanupExpiredSnapshots()
        {
        }
    }
}
