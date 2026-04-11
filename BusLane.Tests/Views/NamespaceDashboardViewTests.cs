namespace BusLane.Tests.Views;

using FluentAssertions;

public class NamespaceDashboardViewTests
{
    [Fact]
    public void Dashboard_UsesDefinedSurfaceTokens()
    {
        // Arrange
        var appXaml = File.ReadAllText(GetAppPath());
        var inboxXaml = File.ReadAllText(GetInboxPath());

        // Assert
        inboxXaml.Should().Contain("LayerBackground");
        appXaml.Should().Contain("x:Key=\"LayerBackground\"");
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
        inboxXaml.Should().NotContain("Classes=\"card\"");
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
