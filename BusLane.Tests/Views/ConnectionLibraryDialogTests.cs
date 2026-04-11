namespace BusLane.Tests.Views;

using FluentAssertions;

public class ConnectionLibraryDialogTests
{
    [Fact]
    public void ConnectionLibraryDialog_UsesSharedDialogRegions()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("Classes=\"dialog-header\"");
        xaml.Should().Contain("Classes=\"dialog-body\"");
    }

    [Fact]
    public void ConnectionLibraryDialog_SeparatesQuickActionsFromFormSurface()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("Classes=\"connection-library-command-bar\"");
        xaml.Should().NotContain("Background=\"{DynamicResource SurfaceSubtle}\"");
    }

    private static string GetDialogPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Views",
            "Dialogs",
            "ConnectionLibraryDialog.axaml"));
    }
}
