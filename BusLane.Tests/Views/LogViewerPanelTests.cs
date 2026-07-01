namespace BusLane.Tests.Views;

using FluentAssertions;
using System.Xml.Linq;

public class LogViewerPanelTests
{
    [Fact]
    public void LogViewerPanel_WhenOpenAnimationIsDefined_ScopesAnimationToPanelSurface()
    {
        // Arrange
        var xaml = File.ReadAllText(GetLogViewerPanelPath());

        // Act
        var document = XDocument.Parse(xaml);
        var styleSelectors = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .Where(selector => selector != null)
            .ToList();
        var borderClasses = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Border")
            .Select(element => element.Attribute("Classes")?.Value)
            .Where(classes => classes != null)
            .ToList();

        // Assert
        borderClasses.Should().Contain("log-viewer-panel-surface");
        styleSelectors.Should().Contain("Border.log-viewer-panel-surface");
        styleSelectors.Should().NotContain("Border");
    }

    private static string GetLogViewerPanelPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Views",
            "Controls",
            "LogViewerPanel.axaml"));
    }
}
