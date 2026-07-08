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
