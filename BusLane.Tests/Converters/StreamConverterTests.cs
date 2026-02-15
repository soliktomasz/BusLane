namespace BusLane.Tests.Converters;

using System.Globalization;
using Avalonia.Media;
using BusLane.Converters;
using BusLane.Models;
using FluentAssertions;

/// <summary>
/// Tests for BoolToColorConverter which uses Fluent 2 theme resources.
/// When App.Current is not available (unit tests), converters return fallback values.
/// </summary>
public class BoolToColorConverterTests
{
    private readonly BoolToColorConverter _sut = new();

    [Fact]
    public void Convert_WithTrue_WhenAppNotInitialized_ReturnsFallbackTransparent()
    {
        // Act
        var result = _sut.Convert(true, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Transparent as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Transparent);
    }

    [Fact]
    public void Convert_WithFalse_WhenAppNotInitialized_ReturnsFallbackTransparent()
    {
        // Act
        var result = _sut.Convert(false, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Transparent as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Transparent);
    }

    [Fact]
    public void Convert_WithNonBool_WhenAppNotInitialized_ReturnsFallbackTransparent()
    {
        // Act
        var result = _sut.Convert("not a bool", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Transparent as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Transparent);
    }

    [Fact]
    public void Convert_AlwaysReturnsSolidColorBrush()
    {
        // Act
        var result = _sut.Convert(true, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<SolidColorBrush>();
    }
}

/// <summary>
/// Tests for BoolToStatusColorConverter which uses Fluent 2 theme resources.
/// When App.Current is not available (unit tests), converters return fallback values.
/// </summary>
public class BoolToStatusColorConverterTests
{
    private readonly BoolToStatusColorConverter _sut = new();

    [Fact]
    public void Convert_WithTrue_WhenAppNotInitialized_ReturnsFallbackGray()
    {
        // Act
        var result = _sut.Convert(true, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Gray as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Gray);
    }

    [Fact]
    public void Convert_WithFalse_WhenAppNotInitialized_ReturnsFallbackGray()
    {
        // Act
        var result = _sut.Convert(false, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Gray as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Gray);
    }

    [Fact]
    public void Convert_WithNonBool_WhenAppNotInitialized_ReturnsFallbackGray()
    {
        // Act
        var result = _sut.Convert("not a bool", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Gray as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Gray);
    }

    [Fact]
    public void Convert_AlwaysReturnsSolidColorBrush()
    {
        // Act
        var result = _sut.Convert(true, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<SolidColorBrush>();
    }
}

/// <summary>
/// Tests for SeverityToBackgroundConverter which uses Fluent 2 theme resources.
/// When App.Current is not available (unit tests), converters return fallback values.
/// </summary>
public class SeverityToBackgroundConverterTests
{
    private readonly SeverityToBackgroundConverter _sut = new();

    [Fact]
    public void Convert_WithInfo_WhenAppNotInitialized_ReturnsFallbackTransparent()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Info, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Transparent as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Transparent);
    }

    [Fact]
    public void Convert_WithWarning_WhenAppNotInitialized_ReturnsFallbackTransparent()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Warning, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Transparent as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Transparent);
    }

    [Fact]
    public void Convert_WithCritical_WhenAppNotInitialized_ReturnsFallbackTransparent()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Critical, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Transparent as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Transparent);
    }

    [Fact]
    public void Convert_WithUnknown_WhenAppNotInitialized_ReturnsFallbackTransparent()
    {
        // Act
        var result = _sut.Convert("unknown", typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert - When App.Current is null, returns Transparent as fallback
        result.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)result!).Color.Should().Be(Colors.Transparent);
    }

    [Fact]
    public void Convert_AlwaysReturnsSolidColorBrush()
    {
        // Act
        var result = _sut.Convert(AlertSeverity.Info, typeof(SolidColorBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<SolidColorBrush>();
    }
}
