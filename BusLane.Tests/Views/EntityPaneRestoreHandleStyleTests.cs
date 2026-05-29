namespace BusLane.Tests.Views;

using FluentAssertions;

public class EntityPaneRestoreHandleStyleTests
{
    [Fact]
    public void MainWindow_EntityPaneRestoreHandle_DoesNotRenderCollapsedRail()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMainWindowPath());

        // Assert
        xaml.Should().NotContain("entity-pane-restore-rail");
        xaml.Should().NotContain("entity-pane-restore-handle");
    }

    [Fact]
    public void MainWindow_CollapsedEntityPane_DoesNotReserveRestoreRailWidth()
    {
        // Arrange
        var code = File.ReadAllText(GetMainWindowCodePath());

        // Assert
        code.Should().NotContain("EntityPaneRestoreHandleWidth");
        code.Should().Contain("new GridLength(0);");
    }

    [Fact]
    public void AppStyles_DoesNotKeepCollapsedEntityRailStyles()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppStylesPath());

        // Assert
        xaml.Should().NotContain("Border.entity-pane-restore-rail");
        xaml.Should().NotContain("Button.entity-pane-restore-handle");
    }

    [Fact]
    public void MessagesPanel_RendersEntityPaneRestoreCommandInHeader()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMessagesPanelPath());

        // Assert
        xaml.Should().Contain("Classes=\"entity-pane-restore-command\"");
        xaml.Should().Contain("Command=\"{Binding ShowEntityPaneCommand}\"");
        xaml.Should().Contain("IsVisible=\"{Binding !IsCurrentEntityPaneVisible}\"");
    }

    [Fact]
    public void AppStyles_EntityPaneRestoreCommand_UsesHeaderIconButtonTreatment()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppStylesPath());

        // Act
        var styleBlock = GetStyleBlock(xaml, "Button.entity-pane-restore-command");

        // Assert
        styleBlock.Should().Contain("<Setter Property=\"Width\" Value=\"36\"/>");
        styleBlock.Should().Contain("<Setter Property=\"Height\" Value=\"36\"/>");
        styleBlock.Should().Contain("<Setter Property=\"Background\" Value=\"{DynamicResource SurfaceSubtle}\"/>");
        styleBlock.Should().Contain("<Setter Property=\"BorderBrush\" Value=\"{DynamicResource BorderDefault}\"/>");
    }

    private static string GetAppStylesPath()
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

    private static string GetMainWindowCodePath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Views",
            "MainWindow.axaml.cs"));
    }

    private static string GetMessagesPanelPath()
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
            "MessagesPanelView.axaml"));
    }

    private static string GetStyleBlock(string xaml, string selector)
    {
        var styleTag = $"<Style Selector=\"{selector}\">";
        var startIndex = xaml.IndexOf(styleTag, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"style '{selector}' should exist in AppStyles.axaml");

        var endIndex = xaml.IndexOf("</Style>", startIndex, StringComparison.Ordinal);
        endIndex.Should().BeGreaterThan(startIndex);

        return xaml[startIndex..(endIndex + "</Style>".Length)];
    }
}
