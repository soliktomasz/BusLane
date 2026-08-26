using BusLane.Models.Dashboard;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using Xunit;

namespace BusLane.Tests.ViewModels.Dashboard;

public class DashboardChartViewModelTests
{
    [Fact]
    public void Constructor_SetsTitleAndNoPlotData()
    {
        // Act
        var vm = new DashboardChartViewModel("Active Messages");

        // Assert
        vm.Title.Should().Be("Active Messages");
        vm.PlotData.Should().BeNull();
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
    public void SetGlobalTimeRange_DoesNotOverrideWhenUsingLocalRange()
    {
        // Arrange
        var vm = new DashboardChartViewModel("Test") { UseGlobalTimeRange = false, SelectedTimeRange = "6 Hours" };

        // Act
        vm.SetGlobalTimeRange("1 Hour");

        // Assert
        vm.SelectedTimeRange.Should().Be("6 Hours");
    }

    [Fact]
    public void UpdateData_SetsLinePlotData()
    {
        // Arrange
        var vm = new DashboardChartViewModel("Test");
        var now = DateTime.Now;
        var points = new[]
        {
            new LinePlotPoint(now.AddMinutes(-5), 10),
            new LinePlotPoint(now, 15)
        };

        // Act
        vm.UpdateData(points);

        // Assert
        vm.PlotData.Should().BeOfType<LinePlotData>();
        var plot = (LinePlotData)vm.PlotData!;
        plot.Points.Should().HaveCount(2);
        plot.LineColorToken.Should().Be("AccentBrand");
    }

    [Fact]
    public void UpdateData_DangerTitle_UsesDangerToken()
    {
        // Arrange
        var vm = new DashboardChartViewModel("Dead Letters Over Time");
        var now = DateTime.Now;

        // Act
        vm.UpdateData(new[] { new LinePlotPoint(now.AddMinutes(-5), 10), new LinePlotPoint(now, 15) });

        // Assert
        ((LinePlotData)vm.PlotData!).LineColorToken.Should().Be("TextDanger");
    }

    [Fact]
    public void UpdateData_SinglePoint_IsNotRenderableTrend()
    {
        // Arrange
        var vm = new DashboardChartViewModel("Test");

        // Act
        vm.UpdateData([new LinePlotPoint(DateTime.Now, 10)]);

        // Assert
        vm.PlotData!.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void UpdateData_SelectedRange_DefinesVisibleWindow()
    {
        // Arrange
        var vm = new DashboardChartViewModel("Test");
        vm.SetGlobalTimeRange("6 Hours");

        // Act
        vm.UpdateData([
            new LinePlotPoint(DateTime.Now.AddMinutes(-1), 10),
            new LinePlotPoint(DateTime.Now, 15)
        ]);

        // Assert
        var plot = vm.PlotData!;
        plot.VisibleStart.Should().NotBeNull();
        plot.VisibleEnd.Should().NotBeNull();
        (plot.VisibleEnd - plot.VisibleStart).Should().Be(TimeSpan.FromHours(6));
    }
}
