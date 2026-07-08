namespace BusLane.Tests.Views;

using System.Xml.Linq;

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

    [Fact]
    public void EntityTreeView_ExposesCreateSubscriptionActionAndDialog()
    {
        // Arrange
        var connectionTreeXaml = File.ReadAllText(GetConnectionTreePath());
        var azureTreeXaml = File.ReadAllText(GetAzureTreePath());
        var dialogXaml = File.ReadAllText(GetSubscriptionCreateDialogPath());

        // Assert
        connectionTreeXaml.Should().Contain("OpenCreateSubscriptionDialogCommand");
        azureTreeXaml.Should().Contain("OpenCreateSubscriptionDialogCommand");
        dialogXaml.Should().Contain("ShowCreateSubscriptionDialog");
        dialogXaml.Should().Contain("CreateSubscriptionCommand");
        dialogXaml.Should().Contain("NewSubscriptionRequiresSession");
    }

    [Fact]
    public void AzureEntityTreeView_ExposesCreateSubscriptionAction()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAzureTreePath());

        // Assert
        xaml.Should().Contain("OpenCreateSubscriptionDialogCommand");
    }

    [Fact]
    public void EntityTreeViews_BindTopicActionsToSettingsVisibility()
    {
        // Arrange
        var connectionTree = File.ReadAllText(GetConnectionTreePath());
        var azureTree = File.ReadAllText(GetAzureTreePath());

        // Assert
        AssertTopicActionVisibilityBindings(connectionTree);
        AssertTopicActionVisibilityBindings(azureTree);
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

    private static string GetSubscriptionCreateDialogPath()
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
            "SubscriptionCreateDialog.axaml"));
    }

    private static void AssertTopicActionVisibilityBindings(string xaml)
    {
        var document = XDocument.Parse(xaml);
        var topicMenuItems = GetMenuItems(document, "Create Subscription");
        var subscriptionMenuItems = GetMenuItems(document, "Delete Subscription");

        FindMenuItem(topicMenuItems, "Details")
            .Attribute("IsVisible")
            .Should()
            .BeNull();
        FindMenuItem(topicMenuItems, "Create Subscription")
            .Attribute("IsVisible")
            .Should()
            .BeNull();
        FindMenuItem(topicMenuItems, "Delete Topic")
            .Attribute("IsVisible")
            .Should()
            .BeNull();
        FindMenuItem(subscriptionMenuItems, "Details")
            .Attribute("IsVisible")
            .Should()
            .BeNull();
        FindMenuItem(subscriptionMenuItems, "Delete Subscription")
            .Attribute("IsVisible")
            .Should()
            .BeNull();

        AssertInlineActionVisibilityBinding(document, "Create subscription");
        AssertInlineActionVisibilityBinding(document, "Subscription details");
        AssertInlineActionVisibilityBinding(document, "Delete subscription");
    }

    private static void AssertInlineActionVisibilityBinding(XDocument document, string tooltip)
    {
        document.Descendants()
            .Single(element => element.Attribute("ToolTip.Tip")?.Value == tooltip)
            .Attribute("IsVisible")
            ?.Value
            .Should()
            .Be("{Binding $parent[Window].DataContext.ShowTopicActionButtons}");
    }

    private static List<XElement> GetMenuItems(XDocument document, string itemHeader)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == "ContextMenu")
            .Select(element => element.Elements().Where(child => child.Name.LocalName == "MenuItem").ToList())
            .Single(items => items.Any(item => item.Attribute("Header")?.Value == itemHeader));
    }

    private static XElement FindMenuItem(IEnumerable<XElement> menuItems, string header)
    {
        return menuItems.Single(element => element.Attribute("Header")?.Value == header);
    }
}
