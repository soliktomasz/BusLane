using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.Dashboard;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BusLane.Tests.ViewModels.Dashboard;

public class NamespaceDashboardViewModelTests
{
    private readonly IDashboardRefreshService _refreshService;
    private readonly IAlertService _alertService;
    private readonly NamespaceInboxViewModel _inboxViewModel;

    public NamespaceDashboardViewModelTests()
    {
        _refreshService = Substitute.For<IDashboardRefreshService>();
        _alertService = Substitute.For<IAlertService>();
        _inboxViewModel = new NamespaceInboxViewModel(
            Substitute.For<INamespaceInboxScoringService>(),
            Substitute.For<INamespaceInboxReviewStore>());
    }

    [Fact]
    public void Constructor_InitializesSubViewModels()
    {
        // Act
        var vm = new NamespaceDashboardViewModel(_refreshService, _alertService, _inboxViewModel);

        // Assert
        vm.ActiveMessagesCard.Should().NotBeNull();
        vm.DeadLetterCard.Should().NotBeNull();
        vm.ScheduledCard.Should().NotBeNull();
        vm.SizeCard.Should().NotBeNull();
        vm.Inbox.Should().NotBeNull();
        vm.TopQueues.Should().NotBeNull();
        vm.TopTopics.Should().NotBeNull();
        vm.Charts.Should().HaveCount(4);
    }

    [Fact]
    public void SelectedTimeRange_DefaultsToOneHour()
    {
        // Act
        var vm = new NamespaceDashboardViewModel(_refreshService, _alertService, _inboxViewModel);

        // Assert
        vm.SelectedTimeRange.Should().Be("1 Hour");
    }

    [Fact]
    public void RefreshedEntities_UpdateInbox()
    {
        // Arrange
        var scoringService = Substitute.For<INamespaceInboxScoringService>();
        scoringService.Rank(Arg.Any<IEnumerable<QueueInfo>>(), Arg.Any<IEnumerable<SubscriptionInfo>>(), Arg.Any<IEnumerable<AlertEvent>>(), Arg.Any<TimeSpan?>())
            .Returns([
                new NamespaceInboxItem(
                    "orders",
                    BusLane.Models.Dashboard.EntityType.Queue,
                    TopicName: null,
                    RequiresSession: false,
                    ActiveMessageCount: 10,
                    DeadLetterCount: 2,
                    ScheduledCount: 0,
                    ActiveAlertCount: 0,
                    Score: 10,
                    Reasons: ["Needs attention"])
            ]);

        var inboxViewModel = new NamespaceInboxViewModel(
            scoringService,
            Substitute.For<INamespaceInboxReviewStore>());

        var vm = new NamespaceDashboardViewModel(_refreshService, _alertService, inboxViewModel);
        vm.SetOperations(null, "namespace-a");

        // Act
        _refreshService.EntitiesUpdated += Raise.Event<EventHandler<NamespaceEntitySnapshot>>(
            this,
            new NamespaceEntitySnapshot(
                [new QueueInfo("orders", 12, 10, 2, 0, 0, DateTimeOffset.UtcNow, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1))],
                [],
                DateTimeOffset.UtcNow));

        // Assert
        vm.Inbox.Items.Should().ContainSingle();
        vm.Inbox.Items.Single().EntityName.Should().Be("orders");
    }
}
