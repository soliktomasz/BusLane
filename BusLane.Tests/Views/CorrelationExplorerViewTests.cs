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
    public void CorrelationExplorer_UsesTimelineFirstBusLaneWorkspace()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));

        // Assert
        xaml.Should().NotContain("Text=\"Correlation Explorer\"");
        xaml.Should().Contain("Classes=\"correlation-command-bar\"");
        xaml.Should().Contain("Kind=\"Search\"");
        xaml.Should().Contain("Kind=\"SlidersHorizontal\"");
        xaml.Should().Contain("Kind=\"RefreshCw\"");
        xaml.Should().Contain("Kind=\"Download\"");
        xaml.Should().Contain("Classes=\"correlation-list\"");
        xaml.Should().Contain("Classes=\"timeline-list\"");
        xaml.Should().Contain("Classes=\"timeline-node\"");
        xaml.Should().Contain("ColumnDefinitions=\"260,360,*\"");
    }

    [Fact]
    public void CorrelationExplorer_UsesBusLaneCommandAndFilterSurfaces()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));
        string[] bindingTokens =
        [
            "Text=\"{Binding FilterText, Mode=TwoWay}\"",
            "Text=\"{Binding FilterFromText}\"",
            "Text=\"{Binding FilterToText}\"",
            "Text=\"{Binding FilterNamespace}\"",
            "Text=\"{Binding FilterEntity}\"",
            "ItemsSource=\"{Binding FilterEnvironmentOptions}\"",
            "SelectedItem=\"{Binding FilterEnvironment}\"",
            "ItemsSource=\"{Binding FilterSourceOptions}\"",
            "SelectedItem=\"{Binding FilterSource}\"",
            "Text=\"{Binding FilterIdentifier}\"",
            "Text=\"{Binding FilterPropertyKey}\"",
            "Text=\"{Binding FilterPropertyValue}\"",
            "Text=\"{Binding FilterValidationMessage}\"",
            "Command=\"{Binding ClearFiltersCommand}\"",
            "Command=\"{Binding ApplyFiltersCommand}\""
        ];

        // Assert
        xaml.Should().NotContain("Text=\"Correlation Explorer\"");
        xaml.Should().Contain("Classes=\"correlation-command-bar\"");
        xaml.Should().Contain("Classes=\"message-search-surface\"");
        xaml.Should().Contain("Kind=\"Search\"");
        xaml.Should().Contain("Kind=\"SlidersHorizontal\"");
        xaml.Should().Contain("Kind=\"RefreshCw\"");
        xaml.Should().Contain("Kind=\"Download\"");
        xaml.Should().Contain("ToolTip.Tip=\"Show structured filters\"");
        xaml.Should().Contain("Classes=\"correlation-filter-surface\"");
        xaml.Should().Contain("ColumnDefinitions=\"*,*,*,*\"");

        foreach (var bindingToken in bindingTokens)
        {
            xaml.Should().Contain(bindingToken);
        }

        xaml.Should().Contain("{DynamicResource TextDanger}");
    }

    [Fact]
    public void CorrelationExplorer_ProvidesInvestigationEmptyStates()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));

        // Assert
        xaml.Should().Contain("No correlations found");
        xaml.Should().Contain("Select a correlation");
        xaml.Should().Contain("Select a message to inspect");
        xaml.Should().Contain("Choose message A and message B");
        xaml.Should().Contain("No replay activity yet");
        xaml.Should().Contain("Converter={StaticResource IntEqualsConverter}");
        xaml.Should().Contain("Converter={x:Static ObjectConverters.IsNull}");
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
        xaml.Should().Contain("ColumnDefinitions=\"*,*,*,*\"");
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

    [Fact]
    public void CorrelationExplorerStyles_UseThemeAwareInvestigationSurfaces()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("..", "Styles", "AppStyles.axaml"));
        string[] selectorTokens =
        [
            "Border.correlation-command-bar",
            "Border.correlation-filter-surface",
            "Border.correlation-pane",
            "Border.correlation-pane.inspector",
            "ListBox.correlation-list",
            "ListBox.timeline-list",
            "Border.correlation-group-row",
            "Border.timeline-event",
            "Ellipse.timeline-node",
            "Border.correlation-inspector-header",
            "ListBox.correlation-list ListBoxItem:pointerover /template/ ContentPresenter",
            "ListBox.correlation-list ListBoxItem:selected /template/ ContentPresenter",
            "ListBox.correlation-list ListBoxItem:selected:pointerover /template/ ContentPresenter",
            "ListBox.timeline-list ListBoxItem:pointerover /template/ ContentPresenter",
            "ListBox.timeline-list ListBoxItem:selected /template/ ContentPresenter",
            "ListBox.timeline-list ListBoxItem:selected:pointerover /template/ ContentPresenter"
        ];
        string[] dynamicResourceTokens =
        [
            "Value=\"{DynamicResource SurfaceSubtle}\"",
            "Value=\"{DynamicResource LayerBackground}\"",
            "Value=\"{DynamicResource CardBackground}\"",
            "Value=\"{DynamicResource BorderDefault}\"",
            "Value=\"{DynamicResource HoverBackground}\"",
            "Value=\"{DynamicResource SelectedBackground}\"",
            "Value=\"{DynamicResource SelectedBorder}\"",
            "Value=\"{DynamicResource AccentBrand}\""
        ];

        // Assert
        foreach (var selectorToken in selectorTokens)
        {
            xaml.Should().Contain(selectorToken);
        }

        const string sectionStartMarker = "<!-- Correlation Explorer -->";
        const string sectionEndMarker = "<!-- /Correlation Explorer -->";
        xaml.Should().Contain(sectionStartMarker);
        xaml.Should().Contain(sectionEndMarker);

        var sectionStart = xaml.IndexOf(sectionStartMarker, StringComparison.Ordinal);
        var sectionEnd = xaml.IndexOf(sectionEndMarker, sectionStart, StringComparison.Ordinal);
        var correlationExplorerStyles = xaml[sectionStart..sectionEnd];

        foreach (var dynamicResourceToken in dynamicResourceTokens)
        {
            correlationExplorerStyles.Should().Contain(dynamicResourceToken);
        }

        correlationExplorerStyles.Should().NotContain("#");
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
