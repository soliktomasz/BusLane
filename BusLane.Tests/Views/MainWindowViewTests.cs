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
