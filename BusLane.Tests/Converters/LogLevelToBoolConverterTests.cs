using BusLane.Converters;
using BusLane.Models.Logging;
using FluentAssertions;
using System.Globalization;

namespace BusLane.Tests.Converters;

public class LogLevelToBoolConverterTests
{
    private readonly LogLevelToBoolConverter _sut = new();

    [Theory]
    [InlineData(LogLevel.Info, "Info", true)]
    [InlineData(LogLevel.Info, "INFO", true)]
    [InlineData(LogLevel.Info, "Warning", false)]
    [InlineData(LogLevel.Warning, "Warning", true)]
    [InlineData(LogLevel.Warning, "WARNING", true)]
    [InlineData(LogLevel.Warning, "Info", false)]
    [InlineData(LogLevel.Error, "Error", true)]
    [InlineData(LogLevel.Error, "ERROR", true)]
    [InlineData(LogLevel.Error, "Info", false)]
    [InlineData(LogLevel.Debug, "Debug", true)]
    [InlineData(LogLevel.Debug, "DEBUG", true)]
    [InlineData(LogLevel.Debug, "Info", false)]
    public void Convert_WithValidLevelAndParameter_ReturnsExpectedResult(LogLevel level, string parameter, bool expected)
    {
        // Act
        var result = _sut.Convert(level, typeof(bool), parameter, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(null, typeof(bool), "Info", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithNullParameter_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(LogLevel.Info, typeof(bool), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithInvalidParameter_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(LogLevel.Info, typeof(bool), "InvalidLevel", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithNonLogLevelValue_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert("NotALogLevel", typeof(bool), "Info", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act & Assert
        var action = () => _sut.ConvertBack(true, typeof(LogLevel), "Info", CultureInfo.InvariantCulture);
        action.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Arrange & Act
        var instance1 = LogLevelToBoolConverter.Instance;
        var instance2 = LogLevelToBoolConverter.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }
}
