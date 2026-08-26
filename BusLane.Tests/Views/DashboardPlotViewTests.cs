namespace BusLane.Tests.Views;

using BusLane.Models.Dashboard;
using BusLane.Views.Controls;
using FluentAssertions;

public class DashboardPlotViewTests
{
    [Fact]
    public void GetEmptyStateText_SinglePoint_ReportsCollectingHistory()
    {
        // Arrange
        var data = new LinePlotData(
            "Active Messages",
            [new LinePlotPoint(DateTime.Now, 10)],
            "AccentBrand");

        // Act
        var text = DashboardPlotView.GetEmptyStateText(data);

        // Assert
        text.Should().Be("Collecting history");
    }

    [Fact]
    public void GetHorizontalLimits_SelectedWindow_UsesWindowBoundaries()
    {
        // Arrange
        var visibleStart = DateTime.Today.AddHours(8);
        var visibleEnd = visibleStart.AddHours(6);
        var data = new LinePlotData(
            "Active Messages",
            [
                new LinePlotPoint(visibleEnd.AddMinutes(-1), 10),
                new LinePlotPoint(visibleEnd, 15)
            ],
            "AccentBrand",
            visibleStart,
            visibleEnd);

        // Act
        var limits = DashboardPlotView.GetHorizontalLimits(
            data,
            data.Points.Select(point => point.Time.ToOADate()).ToArray());

        // Assert
        limits.Minimum.Should().Be(visibleStart.ToOADate());
        limits.Maximum.Should().Be(visibleEnd.ToOADate());
        limits.UsesVisibleWindow.Should().BeTrue();
    }
}
