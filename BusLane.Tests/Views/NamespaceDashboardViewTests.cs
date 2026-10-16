namespace BusLane.Tests.Views;

using System.Xml.Linq;
using FluentAssertions;

public class NamespaceDashboardViewTests
{
    [Fact]
    public void Dashboard_UsesDefinedSurfaceTokens()
    {
        // Arrange
        var appXaml = File.ReadAllText(GetAppPath());
        var stylesXaml = File.ReadAllText(GetStylesPath());

        // Assert
        appXaml.Should().Contain("x:Key=\"LayerBackground\"");
        appXaml.Should().Contain("x:Key=\"DashboardTileBackground\"");
        stylesXaml.Should().Contain("<Style Selector=\"Border.inbox-item-surface\">");
        stylesXaml.Should().Contain("DashboardTileBackground");
    }

    [Fact]
    public void Dashboard_PlacesSearchBeforePriorityAndChartsInAnalytics()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDashboardPath());

        // Act
        var searchIndex = xaml.IndexOf("NamespaceEntitySearchView", StringComparison.Ordinal);
        var inboxIndex = xaml.IndexOf("NamespaceInboxView", StringComparison.Ordinal);
        var chartsIndex = xaml.IndexOf("Charts[0]", StringComparison.Ordinal);

        // Assert
        searchIndex.Should().BeGreaterThanOrEqualTo(0);
        inboxIndex.Should().BeGreaterThan(searchIndex);
        inboxIndex.Should().BeGreaterThanOrEqualTo(0);
        chartsIndex.Should().BeGreaterThan(inboxIndex);
        xaml.Should().Contain("IsVisible=\"{Binding IsAnalyticsSelected}\"");
    }

    [Fact]
    public void Dashboard_UsesCalmOperatorPrimarySurfaces()
    {
        // Arrange
        var dashboard = XDocument.Load(GetDashboardPath());
        var inbox = XDocument.Load(GetInboxPath());

        // Act
        var dashboardClasses = dashboard.Descendants()
            .Select(element => element.Attribute("Classes")?.Value)
            .Where(value => value is not null)
            .ToList();
        var inboxClasses = inbox.Descendants()
            .Select(element => element.Attribute("Classes")?.Value)
            .Where(value => value is not null)
            .ToList();

        // Assert
        dashboardClasses.Should().NotContain("page-header-surface");
        dashboardClasses.Should().Contain("overview-panel");
        inboxClasses.Should().Contain("dashboard-inbox-surface");
        inboxClasses.Should().Contain("inbox-item-surface");
        inboxClasses.Should().NotContain("card");
    }

    [Fact]
    public void Dashboard_UsesSeamedFiveValueHealthStrip()
    {
        // Arrange
        var dashboard = XDocument.Load(GetDashboardPath());

        // Act
        var strip = dashboard.Descendants()
            .Single(element => element.Attribute("Classes")?.Value == "overview-panel overview-metrics");
        var cells = strip.Descendants()
            .Where(element => element.Attribute("Classes")?.Value.StartsWith("overview-metric-cell", StringComparison.Ordinal) == true)
            .ToList();

        // Assert
        strip.Descendants().First(element => element.Name.LocalName == "Grid")
            .Attribute("ColumnDefinitions")!.Value.Should().Be("*,*,*,*,*");
        cells.Should().HaveCount(5);
        cells.Should().ContainSingle(element => element.Attribute("Classes")!.Value == "overview-metric-cell first");
        strip.ToString().Should().Contain("{Binding ScheduledCard}").And.Contain("{Binding SizeCard}");
    }

    [Fact]
    public void Issues_UsesVirtualizedList()
    {
        // Arrange
        var path = GetControlPath("NamespaceIssuesView.axaml");

        // Act
        var xaml = File.ReadAllText(path);

        // Assert
        xaml.Should().Contain("ItemsSource=\"{Binding AllIssues}\"");
        xaml.Should().Contain("<VirtualizingStackPanel/>");
        xaml.Should().Contain("IsVisible=\"{Binding !AllIssues.Count}\"");
        xaml.Should().Contain("Text=\"No issues found.\"");
    }

    [Fact]
    public void InboxActions_AreAlwaysReachableWithoutHover()
    {
        // Arrange
        var xaml = File.ReadAllText(GetInboxPath());

        // Assert
        xaml.Should().Contain("ItemsSource=\"{Binding PriorityItems}\"");
        xaml.Should().NotContain(":pointerover");
        xaml.Should().NotContain("Opacity\" Value=\"0");
        xaml.Should().Contain("OpenDeadLetterCommand");
        xaml.Should().Contain("MarkReviewedCommand");
    }

    [Fact]
    public void Dashboard_ExposesLoadingUpdatingAndAccessibleRetryStates()
    {
        // Arrange
        var path = GetDashboardPath();

        // Act
        var xaml = File.ReadAllText(path);

        // Assert
        xaml.Should().Contain("IsInitialLoading");
        xaml.Should().Contain("Updating namespace data");
        xaml.Should().Contain("RetryFailedSectionCommand");
        xaml.Should().Contain("AutomationProperties.Name=\"Retry failed dashboard section\"");
        xaml.Should().Contain("AutomationProperties.Name=\"Dashboard refresh error\"");
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

    private static string GetDashboardPath()
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
            "NamespaceDashboardView.axaml"));
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

    private static string GetInboxPath()
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
            "NamespaceInboxView.axaml"));
    }

    private static string GetControlPath(string fileName) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "BusLane", "Views", "Controls", fileName));
}
