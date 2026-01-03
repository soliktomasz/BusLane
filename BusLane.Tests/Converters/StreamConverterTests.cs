using System.Globalization;
using Avalonia.Media;
using BusLane.Converters;
using BusLane.Models;
using FluentAssertions;

namespace BusLane.Tests.Converters;

public class BoolToColorConverterTests
{
    private readonly BoolToColorConverter _sut = new();

    [Fact]
    public void Convert_WithTrue_ReturnsGreenishColor()
    {
        // Act
        var result = _sut.Convert(true, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("#FFE8F5E9");
    }

    [Fact]
    public void Convert_WithFalse_ReturnsGrayishColor()
    {
        // Act
        var result = _sut.Convert(false, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("WHITESMOKE");
    }

    [Fact]
    public void Convert_WithNonBool_ReturnsGrayishColor()
    {
        // Act
        var result = _sut.Convert("not a bool", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("WHITESMOKE");
    }
}

public class BoolToStatusColorConverterTests
{
    private readonly BoolToStatusColorConverter _sut = new();

    [Fact]
    public void Convert_WithTrue_ReturnsGreenColor()
    {
        // Act
        var result = _sut.Convert(true, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("#FF4CAF50");
    }

    [Fact]
    public void Convert_WithFalse_ReturnsGrayColor()
    {
        // Act
        var result = _sut.Convert(false, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("#FF9E9E9E");
    }

    [Fact]
    public void Convert_WithNonBool_ReturnsGrayColor()
    {
        // Act
        var result = _sut.Convert("not a bool", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("#FF9E9E9E");
    }
}

public class SeverityToBackgroundConverterTests
{
    private readonly SeverityToBackgroundConverter _sut = new();

    [Fact]
    public void Convert_WithInfo_ReturnsLightBlue()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Info, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("#FFE3F2FD");
    }

    [Fact]
    public void Convert_WithWarning_ReturnsLightOrange()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Warning, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("#FFFFF3E0");
    }

    [Fact]
    public void Convert_WithCritical_ReturnsLightRed()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Critical, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("#FFFFEBEE");
    }

    [Fact]
    public void Convert_WithUnknown_ReturnsLightGray()
    {
        // Act
        var result = _sut.Convert("unknown", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.ToString().ToUpper().Should().Be("WHITESMOKE");
    }
}
