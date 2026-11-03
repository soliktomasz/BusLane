namespace BusLane.Tests.Styling;

using Avalonia.Styling;
using BusLane.Styling;
using FluentAssertions;

public class AppThemesTests
{
    [Theory]
    [InlineData("Light", "Classic", false, "Light")]
    [InlineData("Dark", "Classic", false, "Dark")]
    [InlineData("System", "Classic", true, "Default")]
    [InlineData("Unknown", "Classic", false, "Default")]
    [InlineData(null, null, false, "Default")]
    [InlineData("Light", "Zed", true, "ZedLight")]
    [InlineData("Dark", "Zed", false, "ZedDark")]
    [InlineData("System", "Zed", true, "ZedDark")]
    [InlineData("System", "Zed", false, "ZedLight")]
    public void Resolve_MapsPersistedThemeAndDesignToVariant(string? themeName, string? designName, bool systemIsDark, string expectedKey)
    {
        // Act
        var variant = AppThemes.Resolve(themeName, designName, systemIsDark);

        // Assert
        variant.Key.Should().Be(expectedKey);
    }

    [Fact]
    public void ZedVariants_InheritTheMatchingBaseVariant()
    {
        // Assert
        AppThemes.ZedDark.InheritVariant.Should().Be(ThemeVariant.Dark);
        AppThemes.ZedLight.InheritVariant.Should().Be(ThemeVariant.Light);
    }

    [Theory]
    [InlineData("Dark", true)]
    [InlineData("Light", false)]
    [InlineData("ZedDark", true)]
    [InlineData("ZedLight", false)]
    [InlineData("Default", false)]
    public void IsDark_ResolvesThroughInheritedVariants(string variantKey, bool expected)
    {
        // Arrange
        var variant = variantKey switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            "ZedDark" => AppThemes.ZedDark,
            "ZedLight" => AppThemes.ZedLight,
            _ => ThemeVariant.Default
        };

        // Act
        var isDark = AppThemes.IsDark(variant);

        // Assert
        isDark.Should().Be(expected);
    }
}
