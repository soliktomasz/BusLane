using BusLane.Services.Dashboard;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BusLane.Tests.ViewModels.Dashboard;

public class NamespaceDashboardViewModelTests
{
    private readonly IDashboardRefreshService _refreshService;

    public NamespaceDashboardViewModelTests()
    {
        _refreshService = Substitute.For<IDashboardRefreshService>();
    }

    [Fact]
    public void Constructor_InitializesSubViewModels()
    {
        // Act
        var vm = new NamespaceDashboardViewModel(_refreshService);

        // Assert
        vm.ActiveMessagesCard.Should().NotBeNull();
        vm.DeadLetterCard.Should().NotBeNull();
        vm.ScheduledCard.Should().NotBeNull();
        vm.SizeCard.Should().NotBeNull();
        vm.TopQueues.Should().NotBeNull();
        vm.TopTopics.Should().NotBeNull();
        vm.Charts.Should().HaveCount(6);
    }

    [Fact]
    public void SelectedTimeRange_DefaultsToOneHour()
    {
        // Act
        var vm = new NamespaceDashboardViewModel(_refreshService);

        // Assert
        vm.SelectedTimeRange.Should().Be("1 Hour");
    }
}
