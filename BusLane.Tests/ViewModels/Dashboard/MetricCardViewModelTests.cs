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
        var _sut = new MetricCardViewModel("Active Messages", "messages");

        // Assert
        _sut.Title.Should().Be("Active Messages");
        _sut.Unit.Should().Be("messages");
        _sut.Value.Should().Be(0);
        _sut.TrendPercentage.Should().Be(0);
        _sut.Trend.Should().Be(MetricTrend.Stable);
    }

    [Theory]
    [InlineData(100, 110, 10.0, MetricTrend.Up)]
    [InlineData(100, 90, -10.0, MetricTrend.Down)]
    [InlineData(100, 100, 0.0, MetricTrend.Stable)]
    public void UpdateValue_CalculatesTrend(double previous, double current, double expectedTrend, MetricTrend expectedDirection)
    {
        // Arrange
        var _sut = new MetricCardViewModel("Test", "units");
        _sut.UpdateValue(previous);

        // Act
        _sut.UpdateValue(current);

        // Assert
        _sut.Value.Should().Be(current);
        _sut.TrendPercentage.Should().Be(expectedTrend);
        _sut.Trend.Should().Be(expectedDirection);
    }

    [Theory]
    [InlineData(512, "512.0 MB")]
    [InlineData(1945.6, "1.9 GB")]
    public void ValueDisplay_SizeMetric_IncludesReadableUnit(double valueInMegabytes, string expected)
    {
        // Arrange
        var _sut = new MetricCardViewModel("Total Size", "MB");

        // Act
        _sut.UpdateValue(valueInMegabytes);

        // Assert
        _sut.ValueDisplay.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 110, "+10%", true, false)]
    [InlineData(100, 92.5, "-7.5%", false, true)]
    [InlineData(100, 100, "No change", false, false)]
    public void TrendDisplay_DescribesChangeSincePreviousRefresh(
        double previous, double current, string expected, bool isUp, bool isDown)
    {
        // Arrange
        var sut = new MetricCardViewModel("Test", "units");
        sut.UpdateValue(previous);

        // Act
        sut.UpdateValue(current);

        // Assert
        sut.TrendDisplay.Should().Be(expected);
        sut.IsTrendUp.Should().Be(isUp);
        sut.IsTrendDown.Should().Be(isDown);
    }

    [Fact]
    public void TrendDisplay_BeforeSecondRefresh_IsNoChange()
    {
        // Arrange / Act
        var sut = new MetricCardViewModel("Test", "units");
        sut.UpdateValue(42);

        // Assert
        sut.TrendDisplay.Should().Be("No change");
    }

    [Fact]
    public void UpdateValue_FromZeroToPositive_ReportsUpFromZero()
    {
        // Arrange
        var sut = new MetricCardViewModel("Dead Letters", "messages");
        sut.UpdateValue(0);

        // Act
        sut.UpdateValue(5);

        // Assert
        sut.Trend.Should().Be(MetricTrend.Up);
        sut.IsTrendUp.Should().BeTrue();
        sut.TrendDisplay.Should().Be("Up from 0");
    }

    [Fact]
    public void UpdateValue_AfterUpFromZero_ReturnsToPercentageDisplay()
    {
        // Arrange
        var sut = new MetricCardViewModel("Dead Letters", "messages");
        sut.UpdateValue(0);
        sut.UpdateValue(10);

        // Act
        sut.UpdateValue(11);

        // Assert
        sut.TrendDisplay.Should().Be("+10%");
    }

    [Fact]
    public void UpdateValue_ZeroStaysZero_IsNoChange()
    {
        // Arrange
        var sut = new MetricCardViewModel("Dead Letters", "messages");
        sut.UpdateValue(0);

        // Act
        sut.UpdateValue(0);

        // Assert
        sut.Trend.Should().Be(MetricTrend.Stable);
        sut.TrendDisplay.Should().Be("No change");
    }
}
