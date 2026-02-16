using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
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

    [Fact]
    public void UpdateData_CreatesLineSeriesWithPoints()
    {
        // Arrange
        var vm = new DashboardChartViewModel("Test");
        var now = DateTime.Now;
        var points = new[]
        {
            new DateTimePoint(now.AddMinutes(-5), 10),
            new DateTimePoint(now, 15)
        };

        // Act
        vm.UpdateData(points);

        // Assert
        vm.Series.Should().ContainSingle();
        vm.Series[0].Should().BeOfType<LineSeries<DateTimePoint>>();
    }
}
