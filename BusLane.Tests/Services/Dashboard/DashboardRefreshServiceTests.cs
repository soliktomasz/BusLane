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
