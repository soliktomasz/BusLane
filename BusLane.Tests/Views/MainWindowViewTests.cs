namespace BusLane.Tests.Views;

using FluentAssertions;

public class MainWindowViewTests
{
    [Fact]
    public void MainWindow_StatusArea_UsesNeutralIcon()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMainWindowPath());

        // Assert
        xaml.Should().Contain("<LucideIcon Kind=\"Info\" Size=\"13\"");
        xaml.Should().Contain("Foreground=\"{DynamicResource SubtleForeground}\"");
    }

    [Fact]
    public void MainWindow_StatusArea_OpensPopupOnClick()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMainWindowPath());

        // Assert
        xaml.Should().Contain("Text=\"{Binding ShellStatusSummary}\"");
        xaml.Should().Contain("Command=\"{Binding ToggleStatusPopupCommand}\"");
        xaml.Should().Contain("TextTrimming=\"CharacterEllipsis\"");
    }

    [Fact]
    public void MainWindow_ContainsIntroductionSplashOverlay()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMainWindowPath());

        // Assert
        xaml.Should().Contain("IsVisible=\"{Binding ShowIntroductionSplash}\"");
        xaml.Should().Contain("Text=\"Welcome to BusLane\"");
        xaml.Should().Contain("Text=\"Press Cmd+K to open the command palette\"");
        xaml.Should().Contain("Command=\"{Binding DismissIntroductionSplashCommand}\"");
    }

    [Fact]
    public void MainWindow_HostsScheduledMessagesFeaturePanel()
    {
        var xaml = File.ReadAllText(GetMainWindowPath());

        xaml.Should().Contain("FeaturePanels.ShowScheduledMessages");
        xaml.Should().Contain("FeaturePanels.ScheduledMessagesViewModel");
        xaml.Should().Contain("CloseScheduledMessagesCommand");
        xaml.Should().Contain("<controls:ScheduledMessagesView");
    }

    [Fact]
    public void MainWindow_HostsDashboardAsNamespaceWorkspaceContent()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMainWindowPath());

        // Assert
        xaml.Should().Contain("<controls:NamespaceDashboardView");
        xaml.Should().Contain("IsVisible=\"{Binding IsNamespaceOverviewVisible}\"");
        xaml.Should().NotContain("FeaturePanels.ShowCharts");
        xaml.Should().NotContain("CloseChartsCommand");
    }

    [Fact]
    public void MainWindow_EntityWorkspaceStartsWithOverviewBreadcrumb()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMainWindowPath());
        var breadcrumbXaml = File.ReadAllText(GetControlPath("NamespaceWorkspaceBreadcrumb.axaml"));

        // Assert
        xaml.Should().Contain("<controls:NamespaceWorkspaceBreadcrumb");
        breadcrumbXaml.Should().Contain("BackToOverviewCommand");
    }

    private static string GetMainWindowPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Views",
            "MainWindow.axaml"));
    }

    private static string GetControlPath(string fileName)
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
            fileName));
    }
}
