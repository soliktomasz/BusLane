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

    [Fact]
    public void CorrelationExplorer_ExposesCollapsibleStructuredFilters()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));

        // Assert
        xaml.Should().Contain("ToggleFiltersCommand");
        xaml.Should().Contain("ApplyFiltersCommand");
        xaml.Should().Contain("ClearFiltersCommand");
        xaml.Should().Contain("IsVisible=\"{Binding ShowFilters}\"");
        xaml.Should().Contain("FilterFromText");
        xaml.Should().Contain("FilterToText");
        xaml.Should().Contain("FilterNamespace");
        xaml.Should().Contain("FilterEntity");
        xaml.Should().Contain("FilterEnvironment");
        xaml.Should().Contain("FilterSource");
        xaml.Should().Contain("FilterIdentifier");
        xaml.Should().Contain("FilterPropertyKey");
        xaml.Should().Contain("FilterPropertyValue");
        xaml.Should().Contain("FilterValidationMessage");
        xaml.Should().Contain("RowDefinitions=\"Auto,Auto,*\"");
        xaml.Should().Contain("ColumnDefinitions=\"280,320,*\"");
    }

    [Fact]
    public void CorrelationExplorer_ExposesLiveUpdateIndicatorAndComparisonActions()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));

        // Assert
        xaml.Should().Contain("NewMessageCount");
        xaml.Should().Contain("AcknowledgeNewMessagesCommand");
        xaml.Should().Contain("SetComparisonACommand");
        xaml.Should().Contain("SetComparisonBCommand");
        xaml.Should().Contain("CompareWithPreviousCommand");
        xaml.Should().Contain("ClearComparisonCommand");
        xaml.Should().Contain("Header=\"Compare\"");
        xaml.Should().Contain("ComparisonMessageA.MessageId");
        xaml.Should().Contain("ComparisonMessageB.MessageId");
        xaml.Should().Contain("Comparison.EnqueueTimeDelta");
        xaml.Should().Contain("Comparison.FieldChanges");
        xaml.Should().Contain("Comparison.PropertyChanges");
        xaml.Should().Contain("Comparison.Body.First");
        xaml.Should().Contain("Comparison.Body.Second");
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
