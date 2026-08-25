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
        dashboardClasses.Should().Contain("dashboard-summary-surface");
        inboxClasses.Should().Contain("dashboard-inbox-surface");
        inboxClasses.Should().Contain("inbox-item-surface");
        inboxClasses.Should().NotContain("card");
    }

    [Fact]
    public void Dashboard_UsesThreeValueHealthStrip()
    {
        // Arrange
        var dashboard = XDocument.Load(GetDashboardPath());

        // Act
        var metricGrid = dashboard.Descendants()
            .Single(element =>
                element.Name.LocalName == "Grid"
                && element.Elements().Count(child =>
                    child.Name.LocalName == "Border"
                    && child.Attribute("Classes")?.Value == "dashboard-summary-surface") == 3);

        // Assert
        metricGrid.Attribute("ColumnDefinitions")?.Value.Should().Be("*,*,*");
        metricGrid.Attribute("ColumnSpacing")?.Value.Should().Be("16");
        metricGrid.Elements()
            .Where(element => element.Attribute("Classes")?.Value == "dashboard-summary-surface")
            .Should().OnlyContain(element => element.Attribute("Margin") == null);
    }

    [Fact]
    public void Issues_UsesVirtualizedList()
    {
        var xaml = File.ReadAllText(GetControlPath("NamespaceIssuesView.axaml"));

        xaml.Should().Contain("ItemsSource=\"{Binding AllIssues}\"");
        xaml.Should().Contain("<VirtualizingStackPanel/>");
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
        var xaml = File.ReadAllText(GetDashboardPath());

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
