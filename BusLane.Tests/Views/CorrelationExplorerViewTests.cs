namespace BusLane.Tests.Views;

using FluentAssertions;
using FluentAssertions.Execution;

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
    public void CorrelationWorkspace_UsesContinuousHeaderAndStandardSearchSurface()
    {
        // Arrange
        var mainWindowXaml = File.ReadAllText(GetPath("MainWindow.axaml"));
        var panelStart = mainWindowXaml.IndexOf("<!-- Correlation Explorer Panel -->", StringComparison.Ordinal);
        var panelEnd = mainWindowXaml.IndexOf("<!-- Charts Panel", panelStart, StringComparison.Ordinal);
        var correlationPanel = mainWindowXaml[panelStart..panelEnd];

        var viewXaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));
        var searchStart = viewXaml.LastIndexOf(
            "<TextBox",
            viewXaml.IndexOf("PlaceholderText=\"Search message ID", StringComparison.Ordinal),
            StringComparison.Ordinal);
        var searchEnd = viewXaml.IndexOf("/>", searchStart, StringComparison.Ordinal);
        var searchTextBox = viewXaml[searchStart..searchEnd];

        // Assert
        correlationPanel.Should().Contain("Classes=\"page-header-surface\"");
        using (new AssertionScope())
        {
            searchTextBox.Should().NotContain("Background=\"Transparent\"");
            correlationPanel.Should().NotContain("Margin=\"0,0,0,16\"");
        }
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
    public void CorrelationExplorer_UsesCorrelationRailAndEventTimeline()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));
        string[] expectedTokens =
        [
            "ColumnDefinitions=\"260,360,*\"",
            "Classes=\"correlation-pane\"",
            "Classes=\"correlation-list\"",
            "Classes=\"correlation-group-row\"",
            "Text=\"Correlation\"",
            "Text=\"Session\"",
            "Classes=\"timeline-list\"",
            "Classes=\"timeline-node\"",
            "Classes=\"timeline-event\"",
            "Kind=\"GitBranch\"",
            "ItemsSource=\"{Binding Groups}\"",
            "SelectedItem=\"{Binding SelectedGroup}\"",
            "ItemsSource=\"{Binding Timeline}\"",
            "SelectedItem=\"{Binding SelectedMessage}\"",
            "DisplayId",
            "Messages.Count",
            "Groups.Count",
            "UsesSessionFallback",
            "EnqueuedTime",
            "EntityName",
            "EntityType",
            "Source",
            "Environment",
            "MessageId",
            "SequenceNumber",
            "Command=\"{Binding $parent[UserControl].DataContext.SetComparisonACommand}\"",
            "Command=\"{Binding $parent[UserControl].DataContext.SetComparisonBCommand}\"",
            "CommandParameter=\"{Binding}\"",
            "ToolTip.Tip=\"Use as comparison A\"",
            "ToolTip.Tip=\"Use as comparison B\"",
            "No correlations found",
            "Select a correlation",
            "Converter={StaticResource IntEqualsConverter}"
        ];

        // Assert
        foreach (var expectedToken in expectedTokens)
        {
            xaml.Should().Contain(expectedToken);
        }
    }

    [Fact]
    public void CorrelationExplorer_ProtectsInspectorWidthAndTimelineAccessibility()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));
        string[] expectedTokens =
        [
            "HorizontalScrollBarVisibility=\"Auto\"",
            "VerticalScrollBarVisibility=\"Disabled\"",
            "MinWidth=\"1040\"",
            "<Grid ColumnDefinitions=\"Auto,Auto,*\" ColumnSpacing=\"6\">",
            "Command=\"{Binding $parent[UserControl].DataContext.SetComparisonACommand}\"",
            "Command=\"{Binding $parent[UserControl].DataContext.SetComparisonBCommand}\"",
            "CommandParameter=\"{Binding}\"",
            "StringFormat='Use {0} as comparison A'",
            "StringFormat='Use {0} as comparison B'"
        ];

        // Assert
        foreach (var expectedToken in expectedTokens)
        {
            xaml.Should().Contain(expectedToken);
        }
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
            "Command=\"{Binding ClearSearchCommand}\"",
            "Command=\"{Binding ClearFiltersCommand}\"",
            "Command=\"{Binding ApplyFiltersCommand}\""
        ];
        string[] automationNameTokens =
        [
            "AutomationProperties.Name=\"Search correlation messages\"",
            "AutomationProperties.Name=\"Clear correlation search\"",
            "AutomationProperties.Name=\"From time\"",
            "AutomationProperties.Name=\"To time\"",
            "AutomationProperties.Name=\"Namespace\"",
            "AutomationProperties.Name=\"Entity\"",
            "AutomationProperties.Name=\"Environment\"",
            "AutomationProperties.Name=\"Source\"",
            "AutomationProperties.Name=\"Correlation or session ID\"",
            "AutomationProperties.Name=\"Property key\"",
            "AutomationProperties.Name=\"Property value\""
        ];

        // Assert
        xaml.Should().NotContain("Text=\"Correlation Explorer\"");
        xaml.Should().Contain("Classes=\"correlation-command-bar\"");
        xaml.Should().Contain("Classes=\"message-search-surface\"");
        xaml.Should().Contain("Kind=\"Search\"");
        xaml.Should().Contain("Kind=\"SlidersHorizontal\"");
        xaml.Should().Contain("Kind=\"RefreshCw\"");
        xaml.Should().Contain("Kind=\"Download\"");
        xaml.Should().Contain("ToolTip.Tip=\"Toggle structured filters\"");
        xaml.Should().Contain("Classes=\"correlation-filter-surface\"");
        xaml.Should().Contain("ColumnDefinitions=\"*,*,*,*\"");

        foreach (var bindingToken in bindingTokens)
        {
            xaml.Should().Contain(bindingToken);
        }

        foreach (var automationNameToken in automationNameTokens)
        {
            xaml.Should().Contain(automationNameToken);
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
    public void CorrelationExplorer_UsesStructuredMessageInspector()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));
        string[] expectedTokens =
        [
            "Classes=\"correlation-inspector-header\"",
            "StatusMessage",
            "Classes=\"infobar\"",
            "IsEnabled=\"{Binding SelectedMessage, Converter={x:Static ObjectConverters.IsNotNull}}\"",
            "IsVisible=\"{Binding SelectedMessage, Converter={x:Static ObjectConverters.IsNotNull}}\"",
            "IsVisible=\"{Binding SelectedMessage, Converter={x:Static ObjectConverters.IsNull}}\"",
            "Kind=\"ArrowLeftRight\"",
            "Kind=\"Send\"",
            "Select a message to inspect",
            "Classes=\"property-row\"",
            "SelectedMessage.CorrelationId",
            "SelectedMessage.SessionId",
            "SelectedMessage.EntityName",
            "SelectedMessage.ContentType",
            "SelectedMessage.SequenceNumber",
            "IsVisible=\"{Binding Comparison, Converter={x:Static ObjectConverters.IsNotNull}}\"",
            "IsVisible=\"{Binding Comparison, Converter={x:Static ObjectConverters.IsNull}}\"",
            "Choose message A and message B",
            "No metadata changes",
            "No property changes",
            "No application properties",
            "No replay activity yet",
            "ReplayHistory.Count"
        ];

        // Assert
        foreach (var expectedToken in expectedTokens)
        {
            xaml.Should().Contain(expectedToken);
        }
    }

    [Fact]
    public void CorrelationExplorer_UsesCompactWidthSafeInspectorLayout()
    {
        // Arrange
        var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));
        var styles = File.ReadAllText(GetPath("..", "Styles", "AppStyles.axaml"));
        const string sectionStartMarker = "<!-- Correlation Explorer -->";
        const string sectionEndMarker = "<!-- /Correlation Explorer -->";
        var sectionStart = styles.IndexOf(sectionStartMarker, StringComparison.Ordinal);
        var sectionEnd = styles.IndexOf(sectionEndMarker, sectionStart, StringComparison.Ordinal);
        var correlationExplorerStyles = styles[sectionStart..sectionEnd];
        string[] expectedViewTokens =
        [
            "Classes=\"inspector-tabs\"",
            "Header=\"Properties\"",
            "Header=\"History\"",
            "IsVisible=\"{Binding SelectedMessage, Converter={x:Static ObjectConverters.IsNotNull}}\"",
            "<Grid RowDefinitions=\"Auto,Auto\" RowSpacing=\"10\">",
            "<Grid ColumnDefinitions=\"*,Auto,*\" ColumnSpacing=\"6\">",
            "AutomationProperties.Name=\"Compare selected message with previous\"",
            "AutomationProperties.Name=\"Replay selected message\"",
            "Value A",
            "Value B",
            "Message A body",
            "Message B body"
        ];
        string[] expectedStyleTokens =
        [
            "TabControl.inspector-tabs TabItem",
            "Property=\"Padding\" Value=\"7,10\"",
            "Property=\"MinHeight\" Value=\"38\""
        ];

        // Assert
        foreach (var expectedToken in expectedViewTokens)
        {
            xaml.Should().Contain(expectedToken);
        }

        foreach (var expectedToken in expectedStyleTokens)
        {
            correlationExplorerStyles.Should().Contain(expectedToken);
        }

        xaml.Should().NotContain("ColumnDefinitions=\"130,100,*,*\"");
        xaml.Should().NotContain("Width=\"360\"");
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
