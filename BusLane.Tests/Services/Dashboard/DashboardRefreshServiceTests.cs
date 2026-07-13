using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.Dashboard;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BusLane.Tests.Services.Dashboard;

public class DashboardRefreshServiceTests
{
    private readonly DashboardRefreshService _sut;

    public DashboardRefreshServiceTests()
    {
        _sut = new DashboardRefreshService();
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.LastRefreshTime.Should().BeNull();
        _sut.IsRefreshing.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_TopTopics_UsesSubscriptionMessageCountsNotTopicSize()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        IReadOnlyList<TopEntityInfo>? captured = null;
        _sut.TopEntitiesUpdated += (_, entities) => captured = entities;

        operations.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns([new QueueInfo("queue-a", 10, 10, 0, 0, 100, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns([new TopicInfo("topic-a", sizeInBytes: 999999999, subscriptionCount: 1, accessedAt: null, defaultMessageTtl: TimeSpan.FromMinutes(1))]);
        operations.GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>())
            .Returns([new SubscriptionInfo("sub-a", "topic-a", 25, 25, 0, null, false)]);

        // Act
        await _sut.RefreshAsync("ns", operations);

        // Assert
        captured.Should().NotBeNull();
        var topTopic = captured!.Single(e => e.Type == EntityType.Topic && e.Name == "topic-a");
        topTopic.MessageCount.Should().Be(25);
        topTopic.PercentageOfTotal.Should().Be(100);
    }

    [Fact]
    public async Task RefreshAsync_TopQueues_PercentageIsCalculatedWithinQueuesOnly()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        IReadOnlyList<TopEntityInfo>? captured = null;
        _sut.TopEntitiesUpdated += (_, entities) => captured = entities;

        operations.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns([
                new QueueInfo("queue-a", 80, 80, 0, 0, 100, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)),
                new QueueInfo("queue-b", 20, 20, 0, 0, 100, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
            ]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns([new TopicInfo("topic-a", sizeInBytes: 100, subscriptionCount: 1, accessedAt: null, defaultMessageTtl: TimeSpan.FromMinutes(1))]);
        operations.GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>())
            .Returns([new SubscriptionInfo("sub-a", "topic-a", 200, 200, 0, null, false)]);

        // Act
        await _sut.RefreshAsync("ns", operations);

        // Assert
        captured.Should().NotBeNull();
        var queueA = captured!.Single(e => e.Type == EntityType.Queue && e.Name == "queue-a");
        var queueB = captured.Single(e => e.Type == EntityType.Queue && e.Name == "queue-b");
        queueA.PercentageOfTotal.Should().BeApproximately(80.0, 0.001);
        queueB.PercentageOfTotal.Should().BeApproximately(20.0, 0.001);
    }

    [Fact]
    public async Task StartAutoRefresh_WhenRefreshTakesLongerThanInterval_DoesNotRunOverlappingRefreshes()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var activeRefreshCalls = 0;
        var maxConcurrentRefreshCalls = 0;

        operations.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var inFlight = Interlocked.Increment(ref activeRefreshCalls);
                UpdateMaxValue(ref maxConcurrentRefreshCalls, inFlight);
                await Task.Delay(TimeSpan.FromMilliseconds(120));
                Interlocked.Decrement(ref activeRefreshCalls);

                return
                [
                    new QueueInfo("queue-a", 1, 1, 0, 0, 1, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
                ];
            });
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        operations.GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        _sut.StartAutoRefresh("ns", operations, TimeSpan.FromMilliseconds(20));
        await Task.Delay(TimeSpan.FromMilliseconds(280));
        _sut.StopAutoRefresh();

        // Assert
        maxConcurrentRefreshCalls.Should().Be(1);
    }

    [Fact]
    public async Task StartAutoRefresh_WhenStartedAfterManualRefresh_WaitsUntilIntervalBeforeFirstTick()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var refreshCount = 0;

        operations.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref refreshCount);
                return Task.FromResult<IEnumerable<QueueInfo>>(
                [
                    new QueueInfo("queue-a", 1, 1, 0, 0, 1, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
                ]);
            });
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        operations.GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        try
        {
            // Act
            _sut.StartAutoRefresh("ns", operations, TimeSpan.FromMilliseconds(200));
            await Task.Delay(TimeSpan.FromMilliseconds(75));

            // Assert
            refreshCount.Should().Be(0);
        }
        finally
        {
            _sut.StopAutoRefresh();
        }
    }

    [Fact]
    public async Task RefreshAsync_WithMultipleTopics_FetchesSubscriptionsConcurrently()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var activeSubscriptionCalls = 0;
        var maxConcurrentSubscriptionCalls = 0;

        operations.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                new TopicInfo("topic-a", sizeInBytes: 1, subscriptionCount: 1, accessedAt: null, defaultMessageTtl: TimeSpan.FromMinutes(1)),
                new TopicInfo("topic-b", sizeInBytes: 1, subscriptionCount: 1, accessedAt: null, defaultMessageTtl: TimeSpan.FromMinutes(1)),
                new TopicInfo("topic-c", sizeInBytes: 1, subscriptionCount: 1, accessedAt: null, defaultMessageTtl: TimeSpan.FromMinutes(1))
            ]);
        operations.GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var topicName = callInfo.ArgAt<string>(0);
                var inFlight = Interlocked.Increment(ref activeSubscriptionCalls);
                UpdateMaxValue(ref maxConcurrentSubscriptionCalls, inFlight);
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                Interlocked.Decrement(ref activeSubscriptionCalls);

                return (IEnumerable<SubscriptionInfo>)
                [
                    new SubscriptionInfo($"sub-{topicName}", topicName, 1, 1, 0, null, false)
                ];
            });

        // Act
        await _sut.RefreshAsync("ns", operations);

        // Assert
        maxConcurrentSubscriptionCalls.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task RefreshAsync_WithManyTopics_BoundsConcurrentSubscriptionFetches()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var activeSubscriptionCalls = 0;
        var maxConcurrentSubscriptionCalls = 0;

        operations.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12)
                .Select(i => new TopicInfo($"topic-{i}", sizeInBytes: 1, subscriptionCount: 1, accessedAt: null, defaultMessageTtl: TimeSpan.FromMinutes(1)))
                .ToList());
        operations.GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var topicName = callInfo.ArgAt<string>(0);
                var inFlight = Interlocked.Increment(ref activeSubscriptionCalls);
                UpdateMaxValue(ref maxConcurrentSubscriptionCalls, inFlight);
                await Task.Delay(TimeSpan.FromMilliseconds(40));
                Interlocked.Decrement(ref activeSubscriptionCalls);

                return (IEnumerable<SubscriptionInfo>)
                [
                    new SubscriptionInfo($"sub-{topicName}", topicName, 1, 1, 0, null, false)
                ];
            });

        // Act
        await _sut.RefreshAsync("ns", operations);

        // Assert
        maxConcurrentSubscriptionCalls.Should().BeLessThanOrEqualTo(4);
        await operations.Received(4).GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WithManyTopics_ProgressivelyCompletesCachedSnapshot()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var summaries = new List<NamespaceDashboardSummary>();
        _sut.SummaryUpdated += (_, summary) => summaries.Add(summary);
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns([]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 12)
                .Select(index => new TopicInfo($"topic-{index}", 1, 1, null, TimeSpan.FromMinutes(1)))
                .ToList());
        operations.GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var topicName = callInfo.ArgAt<string>(0);
                return Task.FromResult<IEnumerable<SubscriptionInfo>>(
                [
                    new SubscriptionInfo($"sub-{topicName}", topicName, 1, 1, 0, null, false)
                ]);
            });

        // Act
        await _sut.RefreshAsync("ns", operations);
        await _sut.RefreshAsync("ns", operations);
        await _sut.RefreshAsync("ns", operations);

        // Assert
        summaries.Select(summary => summary.IsPartial).Should().Equal(true, true, false);
        summaries[^1].TotalActiveMessages.Should().Be(12);
        await operations.Received(12).GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenSubscriptionFetchFails_RetriesWithoutCachingEmptyBaseline()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var summaries = new List<NamespaceDashboardSummary>();
        _sut.SummaryUpdated += (_, summary) => summaries.Add(summary);
        operations.GetQueuesAsync(Arg.Any<CancellationToken>()).Returns([]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns([new TopicInfo("topic-a", 1, 1, null, TimeSpan.FromMinutes(1))]);
        operations.GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("temporary failure"),
                _ => Task.FromResult<IEnumerable<SubscriptionInfo>>(
                [
                    new SubscriptionInfo("sub-a", "topic-a", 5, 5, 0, null, false)
                ]));

        // Act
        await _sut.RefreshAsync("ns", operations);
        await _sut.RefreshAsync("ns", operations);

        // Assert
        summaries.Select(summary => summary.IsPartial).Should().Equal(true, false);
        summaries.Select(summary => summary.TotalActiveMessages).Should().Equal(0, 5);
        await operations.Received(2).GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenNamespaceChangesWithoutOperations_DoesNotReusePreviousOperations()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var summaries = new List<NamespaceDashboardSummary>();
        _sut.SummaryUpdated += (_, summary) => summaries.Add(summary);
        operations.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns([new QueueInfo("queue-a", 10, 10, 0, 0, 1, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))]);
        operations.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await _sut.RefreshAsync("namespace-a", operations);
        await _sut.RefreshAsync("namespace-b");

        // Assert
        summaries.Select(summary => summary.TotalActiveMessages).Should().Equal(10, 0);
        await operations.Received(1).GetQueuesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenNamespaceChanges_DoesNotPublishStaleOrMixedSnapshot()
    {
        // Arrange
        var operationsA = Substitute.For<IServiceBusOperations>();
        var operationsB = Substitute.For<IServiceBusOperations>();
        var subscriptionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubscription = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishedSummaries = new List<NamespaceDashboardSummary>();
        _sut.SummaryUpdated += (_, summary) => publishedSummaries.Add(summary);

        operationsA.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns([new QueueInfo("queue-a", 10, 10, 0, 0, 1, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))]);
        operationsA.GetTopicsAsync(Arg.Any<CancellationToken>())
            .Returns([new TopicInfo("topic-a", 1, 1, null, TimeSpan.FromMinutes(1))]);
        operationsA.GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                subscriptionStarted.TrySetResult();
                await releaseSubscription.Task;
                return (IEnumerable<SubscriptionInfo>)
                [
                    new SubscriptionInfo("sub-a", "topic-a", 100, 100, 0, null, false)
                ];
            });

        operationsB.GetQueuesAsync(Arg.Any<CancellationToken>())
            .Returns([new QueueInfo("queue-b", 20, 20, 0, 0, 1, null, false, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))]);
        operationsB.GetTopicsAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var refreshA = _sut.RefreshAsync("namespace-a", operationsA);
        await subscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var refreshB = _sut.RefreshAsync("namespace-b", operationsB);
        releaseSubscription.TrySetResult();
        await Task.WhenAll(refreshA, refreshB);

        // Assert
        publishedSummaries.Should().ContainSingle();
        publishedSummaries[0].TotalActiveMessages.Should().Be(20);
        await operationsA.Received(1).GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>());
        await operationsB.DidNotReceive().GetSubscriptionsAsync("topic-a", Arg.Any<CancellationToken>());
    }

    private static void UpdateMaxValue(ref int target, int candidate)
    {
        while (true)
        {
            var current = target;
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, current) == current)
            {
                return;
            }
        }
    }
}
