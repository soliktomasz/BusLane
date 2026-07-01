namespace BusLane.Tests.Views;

using FluentAssertions;

public class LogViewerPanelTests
{
    [Fact]
    public void LogViewerPanel_ScopesOpenAnimationToPanelSurface()
    {
        // Arrange
        var xaml = File.ReadAllText(GetLogViewerPanelPath());

        // Assert
        xaml.Should().Contain("Classes=\"log-viewer-panel-surface\"");
        xaml.Should().Contain("<Style Selector=\"Border.log-viewer-panel-surface\">");
        xaml.Should().NotContain("<Style Selector=\"Border\">");
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
