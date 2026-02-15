namespace BusLane.Tests.Converters;

using BusLane.Converters;
using BusLane.Models.Logging;
using FluentAssertions;
using System.Globalization;

public class LogSourceToStringConverterTests
{
    private readonly LogSourceToStringConverter _sut = new();

    [Fact]
    public void Convert_Application_ReturnsApp()
    {
        // Act
        var result = _sut.Convert(LogSource.Application, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("APP");
    }

    [Fact]
    public void Convert_ServiceBus_ReturnsSb()
    {
        // Act
        var result = _sut.Convert(LogSource.ServiceBus, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("SB");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public void Convert_UnknownValue_ReturnsDefault(object? value)
    {
        // Act
        var result = _sut.Convert(value, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("APP");
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act & Assert
        var action = () => _sut.ConvertBack("APP", typeof(LogSource), null!, CultureInfo.InvariantCulture);
        action.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Arrange & Act
        var instance1 = LogSourceToStringConverter.Instance;
        var instance2 = LogSourceToStringConverter.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }
}
