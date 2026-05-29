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
        xaml.Should().Contain("VerticalScrollBarVisibility=\"Auto\"");
        xaml.Should().NotContain("ItemsSource=\"{Binding Connection.SavedConnections}\" MaxHeight");
    }

    [Fact]
    public void WelcomeView_RendersEnvironmentForRecentConnections()
    {
        // Arrange
        var xaml = File.ReadAllText(GetWelcomeViewPath());

        // Assert
        xaml.Should().Contain("Classes=\"badge-env\"");
        xaml.Should().Contain("Text=\"{Binding EnvironmentDisplayName}\"");
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
