using BusLane.Models.Dashboard;
using BusLane.ViewModels.Dashboard;
using FluentAssertions;
using Xunit;

namespace BusLane.Tests.ViewModels.Dashboard;

public class MetricCardViewModelTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Act
        var vm = new MetricCardViewModel("Active Messages", "messages");

        // Assert
        vm.Title.Should().Be("Active Messages");
        vm.Unit.Should().Be("messages");
        vm.Value.Should().Be(0);
        vm.TrendPercentage.Should().Be(0);
        vm.Trend.Should().Be(MetricTrend.Stable);
    }

    [Theory]
    [InlineData(100, 110, 10.0, MetricTrend.Up)]
    [InlineData(100, 90, -10.0, MetricTrend.Down)]
    [InlineData(100, 100, 0.0, MetricTrend.Stable)]
    public void UpdateValue_CalculatesTrend(double previous, double current, double expectedTrend, MetricTrend expectedDirection)
    {
        // Arrange
        var vm = new MetricCardViewModel("Test", "units");
        vm.UpdateValue(previous);

        // Act
        vm.UpdateValue(current);

        // Assert
        vm.Value.Should().Be(current);
        vm.TrendPercentage.Should().Be(expectedTrend);
        vm.Trend.Should().Be(expectedDirection);
    }
}
