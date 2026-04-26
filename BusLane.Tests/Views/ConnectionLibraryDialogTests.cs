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

        // Assert — toolbar row combines environment tabs + Add Connection
        xaml.Should().Contain("StartAddConnectionCommand");
        xaml.Should().NotContain("Background=\"{DynamicResource SurfaceSubtle}\"");
    }

    [Fact]
    public void ConnectionLibraryDialog_PlacesBackupSectionInFooter()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Act — Backup section appears after the Saved Connections section
        var savedConnectionsIndex = xaml.IndexOf("Saved Connections", StringComparison.Ordinal);
        var backupIndex = xaml.IndexOf("Backup passphrase", StringComparison.Ordinal);

        // Assert
        savedConnectionsIndex.Should().BeGreaterThanOrEqualTo(0);
        backupIndex.Should().BeGreaterThan(savedConnectionsIndex);
    }

    [Fact]
    public void ConnectionLibraryDialog_PlacesBackupActionsInlineWithPassphraseField()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Act
        var passphraseIndex = xaml.IndexOf("Text=\"Backup passphrase\"", StringComparison.Ordinal);
        var exportIndex = xaml.IndexOf("Text=\"Export\"", StringComparison.Ordinal);
        var importIndex = xaml.IndexOf("Text=\"Import\"", StringComparison.Ordinal);

        // Assert
        passphraseIndex.Should().BeGreaterThanOrEqualTo(0);
        exportIndex.Should().BeGreaterThan(passphraseIndex);
        importIndex.Should().BeGreaterThan(passphraseIndex);
        xaml.Should().Contain("ColumnDefinitions=\"*,Auto,Auto\"");
    }

    [Fact]
    public void ConnectionLibraryDialog_HasBackupManagementSection()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert — backup section exists with management heading
        xaml.Should().Contain("Backup &amp; Management");
        xaml.Should().Contain("Clear All Connections");
    }

    [Fact]
    public void ConnectionLibraryDialog_UsesFixedDialogSize()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("Width=\"900\"");
        xaml.Should().Contain("Height=\"720\"");
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
