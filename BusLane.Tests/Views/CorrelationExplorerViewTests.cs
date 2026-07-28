namespace BusLane.Tests.Views;

using FluentAssertions;

public class CorrelationExplorerViewTests
{
    [Fact]
    public void NavigationSidebar_ExposesExplorerInExpandedAndRailModes()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "NavigationSidebar.axaml"));

        // Assert
        xaml.Split("OpenCorrelationExplorerCommand", StringSplitOptions.None)
            .Should().HaveCountGreaterThanOrEqualTo(3);
        xaml.Should().Contain("Correlation Explorer");
        xaml.Should().Contain("ToolTip.Tip=\"Correlation Explorer\"");
    }

    [Fact]
    public void MainWindow_HostsCorrelationExplorerPanel()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("MainWindow.axaml"));

        // Assert
        xaml.Should().Contain("FeaturePanels.ShowCorrelationExplorer");
        xaml.Should().Contain("FeaturePanels.CorrelationExplorerViewModel");
        xaml.Should().Contain("CloseCorrelationExplorerCommand");
    }

    [Fact]
    public void CorrelationExplorer_ShowsGroupsTimelineDetailsAndHistory()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));

        // Assert
        xaml.Should().Contain("ItemsSource=\"{Binding Groups}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding Timeline}\"");
        xaml.Should().Contain("SelectedMessage.Body");
        xaml.Should().Contain("SelectedMessage.Properties");
        xaml.Should().Contain("ItemsSource=\"{Binding ReplayHistory}\"");
        xaml.Should().Contain("OpenReplayCommand");
        xaml.Should().Contain("ExportHistoryCommand");
    }

    private static string GetPath(params string[] parts)
    {
        return Path.GetFullPath(Path.Combine(
            [
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "BusLane",
                "Views",
                .. parts
            ]));
    }
}
