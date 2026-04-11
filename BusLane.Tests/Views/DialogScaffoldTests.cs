namespace BusLane.Tests.Views;

using FluentAssertions;

public class DialogScaffoldTests
{
    [Fact]
    public void AppStyles_DefineDialogHeaderBodyFooterStyles()
    {
        // Arrange
        var xaml = File.ReadAllText(GetStylesPath());

        // Assert
        xaml.Should().Contain("Border.dialog-header");
        xaml.Should().Contain("Border.dialog-body");
        xaml.Should().Contain("Border.dialog-footer");
    }

    [Fact]
    public void SettingsDialog_UsesSharedDialogRegions()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSettingsDialogPath());

        // Assert
        xaml.Should().Contain("Classes=\"dialog-header\"");
        xaml.Should().Contain("Classes=\"dialog-body\"");
        xaml.Should().Contain("Classes=\"dialog-footer\"");
    }

    [Fact]
    public void SettingsDialog_UsesSharedSettingsRowForRefreshInterval()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSettingsDialogPath());
        var refreshIntervalIndex = xaml.IndexOf("Text=\"Refresh interval\"", StringComparison.Ordinal);

        // Assert
        refreshIntervalIndex.Should().BeGreaterThanOrEqualTo(0);

        // Act
        var rowStart = xaml.LastIndexOf("<Grid", refreshIntervalIndex, StringComparison.Ordinal);
        rowStart.Should().BeGreaterThanOrEqualTo(0);

        var refreshIntervalRow = xaml[rowStart..refreshIntervalIndex];
        refreshIntervalRow.Should().Contain("Classes=\"settings-row\"");
    }

    private static string GetStylesPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Styles",
            "AppStyles.axaml"));
    }

    private static string GetSettingsDialogPath()
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
            "SettingsDialog.axaml"));
    }
}
