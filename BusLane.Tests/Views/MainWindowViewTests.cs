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
}
