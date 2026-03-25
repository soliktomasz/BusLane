namespace BusLane.Tests.Views;

using FluentAssertions;

public class WelcomeViewTests
{
    [Fact]
    public void WelcomeView_DoesNotRenderMyConnectionsButton()
    {
        // Arrange
        var xaml = File.ReadAllText(GetWelcomeViewPath());

        // Act
        var hasConnectionLibraryButton = xaml.Contains("Command=\"{Binding OpenConnectionLibraryCommand}\"", StringComparison.Ordinal);

        // Assert
        hasConnectionLibraryButton.Should().BeFalse();
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
