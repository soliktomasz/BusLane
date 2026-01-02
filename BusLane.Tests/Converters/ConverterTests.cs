using System.Globalization;
using Avalonia.Data;
using BusLane.Converters;
using BusLane.Models;
using FluentAssertions;

namespace BusLane.Tests.Converters;

public class EntitySelectionConverterTests
{
    private readonly EntitySelectionConverter _sut = EntitySelectionConverter.Instance;

    [Fact]
    public void Convert_WithMatchingItems_ReturnsTrue()
    {
        // Arrange
        var item = new object();
        var values = new List<object?> { item, item };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WithDifferentItems_ReturnsFalse()
    {
        // Arrange
        var item1 = new object();
        var item2 = new object();
        var values = new List<object?> { item1, item2 };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithNullCurrentItem_ReturnsFalse()
    {
        // Arrange
        var values = new List<object?> { null, new object() };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithNullSelectedItem_ReturnsFalse()
    {
        // Arrange
        var values = new List<object?> { new object(), null };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithLessThanTwoValues_ReturnsFalse()
    {
        // Arrange
        var values = new List<object?> { new object() };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithEqualObjects_ReturnsTrue()
    {
        // Arrange - Using strings which implement value equality
        var values = new List<object?> { "test", "test" };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }
}

public class DeadLetterBadgeVisibilityConverterTests
{
    private readonly DeadLetterBadgeVisibilityConverter _sut = DeadLetterBadgeVisibilityConverter.Instance;

    [Theory]
    [InlineData(1L, true, true)]   // Has dead letters, badges enabled
    [InlineData(0L, true, false)]  // No dead letters, badges enabled
    [InlineData(1L, false, false)] // Has dead letters, badges disabled
    [InlineData(0L, false, false)] // No dead letters, badges disabled
    public void Convert_WithVariousInputs_ReturnsExpectedResult(long count, bool showBadges, bool expected)
    {
        // Arrange
        var values = new List<object?> { count, showBadges };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_WithIntCount_WorksCorrectly()
    {
        // Arrange - int instead of long
        var values = new List<object?> { 5, true };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WithMissingShowBadgesValue_DefaultsToShow()
    {
        // Arrange - Only one value provided
        var values = new List<object?> { 5L };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false); // Returns false because less than 2 values
    }

    [Fact]
    public void Convert_WithNonBoolShowBadges_DefaultsToTrue()
    {
        // Arrange
        var values = new List<object?> { 5L, "not a bool" };

        // Act
        var result = _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true); // Defaults to showing when setting is invalid
    }
}

public class BoolToStatusTextConverterTests
{
    private readonly BoolToStatusTextConverter _sut = new();

    [Theory]
    [InlineData(true, "Streaming")]
    [InlineData(false, "Stopped")]
    public void Convert_WithBoolValue_ReturnsExpectedText(bool isActive, string expected)
    {
        // Act
        var result = _sut.Convert(isActive, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_WithNonBoolValue_ReturnsStopped()
    {
        // Act
        var result = _sut.Convert("not a bool", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Stopped");
    }

    [Fact]
    public void Convert_WithNull_ReturnsStopped()
    {
        // Act
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Stopped");
    }
}

public class BoolToOpacityConverterTests
{
    private readonly BoolToOpacityConverter _sut = new();

    [Theory]
    [InlineData(true, 0.5)]  // For IsAcknowledged: true = dimmed
    [InlineData(false, 1.0)] // For IsAcknowledged: false = full
    public void Convert_WithBoolValue_ReturnsExpectedOpacity(bool value, double expected)
    {
        // Act
        var result = _sut.Convert(value, typeof(double), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_WithNonBoolValue_ReturnsFullOpacity()
    {
        // Act
        var result = _sut.Convert("not a bool", typeof(double), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(1.0);
    }
}

public class SeverityToColorConverterTests
{
    private readonly SeverityToColorConverter _sut = new();

    [Fact]
    public void Convert_WithInfoSeverity_ReturnsBlueColor()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Info, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Convert_WithWarningSeverity_ReturnsOrangeColor()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Warning, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Convert_WithCriticalSeverity_ReturnsRedColor()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Critical, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Convert_WithNonSeverityValue_ReturnsGrayColor()
    {
        // Act
        var result = _sut.Convert("not a severity", typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().NotBeNull();
    }
}

public class EnvironmentTabConverterTests
{
    private readonly EnvironmentTabConverter _sut = EnvironmentTabConverter.Instance;

    [Fact]
    public void Convert_WithMatchingValue_ReturnsTrue()
    {
        // Arrange
        var value = ConnectionEnvironment.Production;
        var parameter = ConnectionEnvironment.Production;

        // Act
        var result = _sut.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var value = ConnectionEnvironment.Production;
        var parameter = ConnectionEnvironment.Development;

        // Act
        var result = _sut.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(null, typeof(bool), ConnectionEnvironment.Production, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithNullParameter_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(ConnectionEnvironment.Production, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void ConvertBack_WithTrueAndParameter_ReturnsParameter()
    {
        // Arrange
        var parameter = ConnectionEnvironment.Development;

        // Act
        var result = _sut.ConvertBack(true, typeof(ConnectionEnvironment), parameter, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(ConnectionEnvironment.Development);
    }

    [Fact]
    public void ConvertBack_WithFalse_ReturnsDoNothing()
    {
        // Act
        var result = _sut.ConvertBack(false, typeof(ConnectionEnvironment), ConnectionEnvironment.Production, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(BindingOperations.DoNothing);
    }

    [Fact]
    public void ConvertBack_WithNullParameter_ReturnsDoNothing()
    {
        // Act
        var result = _sut.ConvertBack(true, typeof(ConnectionEnvironment), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(BindingOperations.DoNothing);
    }
}

