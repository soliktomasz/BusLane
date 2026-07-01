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
    public void MainWindow_StatusArea_ShowsFullShellStatusWithoutPopupTrigger()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMainWindowPath());

        // Assert
        xaml.Should().Contain("Text=\"{Binding ShellStatusMessage}\"");
        xaml.Should().Contain("TextWrapping=\"Wrap\"");
        xaml.Should().NotContain("ToggleStatusPopupCommand");
        xaml.Should().NotContain("Click to see full message");
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
