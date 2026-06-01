namespace BusLane.Tests.Views;

using FluentAssertions;

public class EntityTreeViewTests
{
    [Fact]
    public void AppStyles_EntityListItemsRemainFocusable()
    {
        // Arrange
        var xaml = File.ReadAllText(GetStylesPath());

        // Assert
        xaml.Should().NotContain("<Setter Property=\"Focusable\" Value=\"False\"/>");
    }

    [Fact]
    public void EntityTreeViews_UseSharedSearchSurfaceClass()
    {
        // Assert
        File.ReadAllText(GetConnectionTreePath()).Should().Contain("Classes=\"pane-search-surface\"");
        File.ReadAllText(GetAzureTreePath()).Should().Contain("Classes=\"pane-search-surface\"");
    }

    [Fact]
    public void EntityTreeViews_ExposePinningControls()
    {
        // Arrange
        var connectionTree = File.ReadAllText(GetConnectionTreePath());
        var azureTree = File.ReadAllText(GetAzureTreePath());

        // Assert
        connectionTree.Should().Contain("ToggleSelectedEntityPinCommand");
        connectionTree.Should().Contain("CurrentNavigation.PinnedEntities");
        connectionTree.Should().Contain("SelectPinnedEntityCommand");
        azureTree.Should().Contain("ToggleSelectedEntityPinCommand");
        azureTree.Should().Contain("CurrentNavigation.PinnedEntities");
        azureTree.Should().Contain("SelectPinnedEntityCommand");
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

    private static string GetConnectionTreePath()
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
            "EntityTreeView.axaml"));
    }

    private static string GetAzureTreePath()
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
            "AzureEntityTreeView.axaml"));
    }
}
