namespace BusLane.Tests.Views;

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
    public void Dashboard_PlacesPriorityInboxBeforeSecondaryWidgets()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDashboardPath());

        // Act
        var inboxIndex = xaml.IndexOf("NamespaceInboxView", StringComparison.Ordinal);
        var chartsIndex = xaml.IndexOf("ItemsControl Grid.Row=\"4\"", StringComparison.Ordinal);

        // Assert
        inboxIndex.Should().BeGreaterThanOrEqualTo(0);
        chartsIndex.Should().BeGreaterThan(inboxIndex);
    }

    [Fact]
    public void Dashboard_UsesCalmerPrimarySurfaces()
    {
        // Arrange
        var dashboardXaml = File.ReadAllText(GetDashboardPath());
        var inboxXaml = File.ReadAllText(GetInboxPath());

        // Assert
        dashboardXaml.Should().Contain("Classes=\"page-header-surface\"");
        dashboardXaml.Should().NotContain("Classes=\"card\"");
        inboxXaml.Should().Contain("Classes=\"dashboard-inbox-surface\"");
        inboxXaml.Should().Contain("Classes=\"inbox-item-surface\"");
        inboxXaml.Should().NotContain("Classes=\"card\"");
        inboxXaml.Should().NotContain("Padding=\"20\"");
        inboxXaml.Should().NotContain("Background=\"{DynamicResource LayerBackground}\"");
    }

    [Fact]
    public void Dashboard_UsesSharedMetricGridSpacing()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDashboardPath());

        // Assert
        xaml.Should().Contain("<Grid Grid.Row=\"2\" ColumnDefinitions=\"*,*,*,*\" ColumnSpacing=\"10\" Margin=\"0,0,0,20\">");
        xaml.Should().NotContain("Classes=\"dashboard-summary-surface\" Margin=");
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
}
