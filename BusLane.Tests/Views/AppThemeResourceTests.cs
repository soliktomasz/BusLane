namespace BusLane.Tests.Views;

using System.Xml.Linq;

using FluentAssertions;

public class AppThemeResourceTests
{
    [Fact]
    public void AppResources_DefineSharedDashboardSurfaceTokens()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppPath());

        // Assert
        xaml.Should().Contain("x:Key=\"LayerBackground\"");
        xaml.Should().Contain("x:Key=\"DashboardTileBackground\"");
        xaml.Should().Contain("x:Key=\"AccentBrandSubtle\"");
    }

    [Fact]
    public void AppStyles_DefineSharedToggleAndMenuStyles()
    {
        // Arrange
        var xaml = File.ReadAllText(GetStylesPath());

        // Assert
        xaml.Should().Contain("<Style Selector=\"ToggleSwitch\">");
        xaml.Should().Contain("<Style Selector=\"CheckBox\">");
        xaml.Should().Contain("<Style Selector=\"ContextMenu\">");
        xaml.Should().Contain("<Style Selector=\"MenuItem\">");
    }

    [Fact]
    public void AppStyles_PanelToggleUsesMinimumAccessibleHitTarget()
    {
        // Arrange
        var xaml = File.ReadAllText(GetStylesPath());

        // Assert
        xaml.Should().Contain("<Setter Property=\"MinWidth\" Value=\"36\"/>");
        xaml.Should().Contain("<Setter Property=\"MinHeight\" Value=\"36\"/>");
    }

    [Fact]
    public void AppStyles_BoxShadowSettersUseConcreteValues()
    {
        // Arrange
        var document = XDocument.Parse(File.ReadAllText(GetStylesPath()));

        // Act
        var invalidSetters = document.Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Where(element => element.Attribute("Property")?.Value == "BoxShadow")
            .Where(element =>
            {
                var value = element.Attribute("Value")?.Value;
                return string.IsNullOrWhiteSpace(value) || value == "{x:Null}";
            })
            .ToList();

        // Assert
        invalidSetters.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ZedDark")]
    [InlineData("ZedLight")]
    public void AppResources_DeclareZedThemeDictionary(string variant)
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppPath());

        // Assert
        xaml.Should().Contain($"x:Key=\"{{x:Static styling:AppThemes.{variant}}}\"");
        xaml.Should().Contain($"avares://BusLane/Styles/Themes/{variant}.axaml");
    }

    [Theory]
    [InlineData("ZedDark", "Dark")]
    [InlineData("ZedLight", "Light")]
    public void ZedTheme_DefinesEveryTokenItsBaseThemeDefines(string zedVariant, string baseVariant)
    {
        // Arrange
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var baseKeys = XDocument.Parse(File.ReadAllText(GetAppPath()))
            .Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary"
                              && (string?)element.Attribute(x + "Key") == baseVariant)
            .SelectMany(element => element.Elements())
            .Select(element => (string?)element.Attribute(x + "Key"))
            .OfType<string>()
            .ToHashSet();
        var zedKeys = XDocument.Parse(File.ReadAllText(GetZedThemePath(zedVariant)))
            .Root!.Elements()
            .Select(element => (string?)element.Attribute(x + "Key"))
            .OfType<string>()
            .ToHashSet();

        // Assert
        baseKeys.Should().NotBeEmpty();
        zedKeys.Should().Contain(baseKeys, $"every token the {baseVariant} theme defines must have a {zedVariant} value");
    }

    [Fact]
    public void ZedDesign_OnlyOverridesSelectorsTheBaseStylesDefine()
    {
        // Arrange - an override for a selector AppStyles lacks would silently match nothing
        var baseSelectors = XDocument.Parse(File.ReadAllText(GetStylesPath()))
            .Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => (string?)element.Attribute("Selector"))
            .OfType<string>()
            .ToHashSet();
        var zedSelectors = XDocument.Parse(File.ReadAllText(GetZedDesignPath()))
            .Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => (string?)element.Attribute("Selector"))
            .OfType<string>()
            .ToList();

        // Assert
        zedSelectors.Should().NotBeEmpty();
        zedSelectors.Where(selector => !baseSelectors.Contains(selector) && !selector.Contains("/template/"))
            .Should().BeEmpty();
    }

    private static string GetAppPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "App.axaml"));
    }

    private static string GetZedThemePath(string variant)
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Styles",
            "Themes",
            $"{variant}.axaml"));
    }

    private static string GetZedDesignPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Styles",
            "Designs",
            "Zed.axaml"));
    }

    private static string GetStylesPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Styles",
            "AppStyles.axaml"));
    }
}
