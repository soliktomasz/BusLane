using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using LiveChartsCore;
using Xunit;

namespace BusLane.Tests.ViewModels.Dashboard;

public class DashboardChartViewModelTests
{
    [Fact]
    public void Constructor_SetsTitleAndInitializesSeries()
    {
        // Act
        var vm = new DashboardChartViewModel("Active Messages");

        // Assert
        vm.Title.Should().Be("Active Messages");
        vm.Series.Should().BeEmpty();
        vm.TimeRangeOptions.Should().NotBeEmpty();
    }

    [Fact]
    public void SetGlobalTimeRange_UpdatesSelectedTimeRange()
    {
        // Arrange
        var vm = new DashboardChartViewModel("Test");

        // Act
        vm.SetGlobalTimeRange("1 Hour");

        // Assert
        vm.SelectedTimeRange.Should().Be("1 Hour");
    }
}
