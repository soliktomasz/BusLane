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

    [Fact]
    public void ConnectionLibraryDialog_LeftAlignsBackupPassphraseSection()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("HorizontalAlignment=\"Left\"");
    }

    [Fact]
    public void ConnectionLibraryDialog_PlacesBackupActionsInlineWithPassphraseField()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Act
        var passphraseIndex = xaml.IndexOf("Text=\"Backup passphrase\"", StringComparison.Ordinal);
        var exportIndex = xaml.IndexOf("Text=\"Export Backup\"", StringComparison.Ordinal);
        var importIndex = xaml.IndexOf("Text=\"Import Backup\"", StringComparison.Ordinal);

        // Assert
        passphraseIndex.Should().BeGreaterThanOrEqualTo(0);
        exportIndex.Should().BeGreaterThan(passphraseIndex);
        importIndex.Should().BeGreaterThan(passphraseIndex);
        xaml.Should().Contain("ColumnDefinitions=\"*,Auto,Auto\"");
    }

    [Fact]
    public void ConnectionLibraryDialog_UsesWiderBackupPassphraseLayout()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("MaxWidth=\"520\"");
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
