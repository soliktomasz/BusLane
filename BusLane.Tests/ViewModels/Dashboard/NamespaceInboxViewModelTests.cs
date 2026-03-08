namespace BusLane.Tests.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;

public class NamespaceInboxViewModelTests
{
    private readonly FakeNamespaceInboxScoringService _scoringService = new();
    private readonly InMemoryNamespaceInboxReviewStore _reviewStore = new();

    [Fact]
    public void Refresh_BuildsRowsFromQueuesAndSubscriptions()
    {
        // Arrange
        _scoringService.Items =
        [
            CreateInboxItem("orders", EntityType.Queue, activeMessageCount: 12, deadLetterCount: 3),
            CreateInboxItem("topic-a/sub-a", EntityType.Subscription, activeMessageCount: 8, deadLetterCount: 1, topicName: "topic-a")
        ];

        var sut = new NamespaceInboxViewModel(_scoringService, _reviewStore, _ => { }, _ => { }, _ => { });

        // Act
        sut.Refresh("namespace-a", [CreateQueueInfo("orders", 12, 3)], [CreateSubscriptionInfo("topic-a", "sub-a", 8, 1)], []);

        // Assert
        sut.Items.Should().HaveCount(2);
        sut.Items.Select(item => item.EntityName).Should().Contain(["orders", "topic-a/sub-a"]);
    }

    [Fact]
    public void MarkReviewed_UpdatesDeltaBaseline()
    {
        // Arrange
        _scoringService.Items = [CreateInboxItem("orders", EntityType.Queue, activeMessageCount: 12, deadLetterCount: 3)];
        var sut = new NamespaceInboxViewModel(_scoringService, _reviewStore, _ => { }, _ => { }, _ => { });

        sut.Refresh("namespace-a", [CreateQueueInfo("orders", 12, 3)], [], []);
        var item = sut.Items.Single();

        // Act
        item.MarkReviewedCommand.Execute(null);

        _scoringService.Items = [CreateInboxItem("orders", EntityType.Queue, activeMessageCount: 20, deadLetterCount: 5)];
        sut.Refresh("namespace-a", [CreateQueueInfo("orders", 20, 5)], [], []);

        // Assert
        var refreshedItem = sut.Items.Single();
        refreshedItem.ActiveMessageDelta.Should().Be(8);
        refreshedItem.DeadLetterDelta.Should().Be(2);
    }

    [Fact]
    public void OpenCommands_InvokeExpectedNavigationCallbacks()
    {
        // Arrange
        NamespaceInboxItem? openedMessages = null;
        NamespaceInboxItem? openedDeadLetter = null;
        NamespaceInboxItem? openedSessionInspector = null;

        _scoringService.Items =
        [
            CreateInboxItem("orders", EntityType.Queue, activeMessageCount: 12, deadLetterCount: 3, requiresSession: true)
        ];

        var sut = new NamespaceInboxViewModel(
            _scoringService,
            _reviewStore,
            item => openedMessages = item,
            item => openedDeadLetter = item,
            item => openedSessionInspector = item);

        sut.Refresh("namespace-a", [CreateQueueInfo("orders", 12, 3, requiresSession: true)], [], []);
        var item = sut.Items.Single();

        // Act
        item.OpenMessagesCommand.Execute(null);
        item.OpenDeadLetterCommand.Execute(null);
        item.OpenSessionInspectorCommand.Execute(null);

        // Assert
        openedMessages.Should().NotBeNull();
        openedDeadLetter.Should().NotBeNull();
        openedSessionInspector.Should().NotBeNull();
        openedMessages!.EntityName.Should().Be("orders");
        openedDeadLetter!.EntityName.Should().Be("orders");
        openedSessionInspector!.EntityName.Should().Be("orders");
    }

    [Fact]
    public void Refresh_WithoutHistoricalData_UsesNeutralDeltas()
    {
        // Arrange
        _scoringService.Items = [CreateInboxItem("orders", EntityType.Queue, activeMessageCount: 12, deadLetterCount: 3)];
        var sut = new NamespaceInboxViewModel(_scoringService, _reviewStore, _ => { }, _ => { }, _ => { });

        // Act
        sut.Refresh("namespace-a", [CreateQueueInfo("orders", 12, 3)], [], []);

        // Assert
        var item = sut.Items.Single();
        item.ActiveMessageDelta.Should().Be(0);
        item.DeadLetterDelta.Should().Be(0);
        item.AlertDelta.Should().Be(0);
    }

    [Fact]
    public void ToggleExpanded_DefaultsExpandedAndTogglesState()
    {
        // Arrange
        var sut = new NamespaceInboxViewModel(_scoringService, _reviewStore, _ => { }, _ => { }, _ => { });

        // Act
        var initialState = sut.IsExpanded;
        sut.ToggleExpandedCommand.Execute(null);

        // Assert
        initialState.Should().BeTrue();
        sut.IsExpanded.Should().BeFalse();
        sut.ExpandButtonText.Should().Be("Expand");
    }

    private static NamespaceInboxItem CreateInboxItem(
        string entityName,
        EntityType entityType,
        long activeMessageCount,
        long deadLetterCount,
        string? topicName = null,
        bool requiresSession = false)
    {
        return new NamespaceInboxItem(
            entityName,
            entityType,
            topicName,
            requiresSession,
            ActiveMessageCount: activeMessageCount,
            DeadLetterCount: deadLetterCount,
            ScheduledCount: 0,
            ActiveAlertCount: 1,
            Score: 10,
            Reasons: ["Needs attention"]);
    }

    private static QueueInfo CreateQueueInfo(
        string name,
        long activeMessageCount,
        long deadLetterCount,
        bool requiresSession = false)
    {
        return new QueueInfo(
            name,
            MessageCount: activeMessageCount + deadLetterCount,
            ActiveMessageCount: activeMessageCount,
            DeadLetterCount: deadLetterCount,
            ScheduledCount: 0,
            SizeInBytes: 0,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: requiresSession,
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

    private sealed class FakeNamespaceInboxScoringService : INamespaceInboxScoringService
    {
        public IReadOnlyList<NamespaceInboxItem> Items { get; set; } = [];

        public IReadOnlyList<NamespaceInboxItem> Rank(
            IEnumerable<QueueInfo> queues,
            IEnumerable<SubscriptionInfo> subscriptions,
            IEnumerable<AlertEvent> activeAlerts,
            TimeSpan? comparisonWindow = null)
        {
            return Items;
        }
    }

    private sealed class InMemoryNamespaceInboxReviewStore : INamespaceInboxReviewStore
    {
        private readonly List<NamespaceInboxReviewState> _states = [];

        public IReadOnlyList<NamespaceInboxReviewState> LoadAll() => _states.ToList();

        public NamespaceInboxReviewState? Get(string namespaceId, string entityName)
        {
            return _states.FirstOrDefault(state => state.NamespaceId == namespaceId && state.EntityName == entityName);
        }

        public void Save(NamespaceInboxReviewState reviewState)
        {
            var existing = Get(reviewState.NamespaceId, reviewState.EntityName);
            if (existing != null)
            {
                _states.Remove(existing);
            }

            _states.Add(reviewState);
        }
    }
}
