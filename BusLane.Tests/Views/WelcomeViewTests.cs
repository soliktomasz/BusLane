namespace BusLane.Tests.Views;

using FluentAssertions;

public class WelcomeViewTests
{
    [Fact]
    public void WelcomeView_UsesPrimaryStartSurface()
    {
        // Arrange
        var xaml = File.ReadAllText(GetWelcomeViewPath());

        // Assert
        xaml.Should().Contain("Classes=\"welcome-primary-surface\"");
        xaml.Should().Contain("Command=\"{Binding OpenConnectionLibraryCommand}\"");
    }

    [Fact]
    public void WelcomeView_RendersRecentConnectionsInSecondarySurface()
    {
        // Arrange
        var xaml = File.ReadAllText(GetWelcomeViewPath());

        // Assert
        xaml.Should().Contain("Classes=\"welcome-recent-surface\"");
    }

    private static string GetWelcomeViewPath()
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
            "WelcomeView.axaml"));
    }
}
