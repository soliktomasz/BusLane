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
    public void EntityTreeView_CollapsesGlobalOperationsIntoHeaderFlyout()
    {
        // Arrange
        var document = XDocument.Parse(File.ReadAllText(GetConnectionTreePath()));

        // Assert
        var overflowButton = document.Descendants()
            .Single(element => element.Name.LocalName == "Button" &&
                               element.Attribute("ToolTip.Tip")?.Value == "More entity actions");

        overflowButton.Descendants()
            .Single(element => element.Name.LocalName == "Flyout")
            .Attribute("Placement")
            ?.Value
            .Should()
            .Be("BottomEdgeAlignedRight");

        overflowButton.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Command")?.Value)
            .Should()
            .Contain([
                "{Binding EntityOperations.OpenCreateQueueDialogCommand}",
                "{Binding EntityOperations.OpenCreateTopicDialogCommand}",
                "{Binding ExportNamespaceTopologyCommand}",
                "{Binding ImportNamespaceTopologyCommand}"
            ]);

        GetHeaderTooltips(document).Should().NotContain([
            "Create queue",
            "Create topic",
            "Export namespace topology",
            "Import namespace topology"
        ]);
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

    [Fact]
    public void EntityTreeViews_DoNotShowGlobalLoadingIndicatorsInsideEntityGroups()
    {
        // Arrange
        var connectionTree = XDocument.Parse(File.ReadAllText(GetConnectionTreePath()));
        var azureTree = XDocument.Parse(File.ReadAllText(GetAzureTreePath()));

        // Assert
        AssertEntityGroupLoadingDoesNotBindToGlobalLoading(connectionTree);
        AssertEntityGroupLoadingDoesNotBindToGlobalLoading(azureTree);
    }

    [Fact]
    public void AppStyles_EntityListItemsSuppressSelectionChrome()
    {
        // Arrange
        var xaml = File.ReadAllText(GetStylesPath());

        // Assert
        xaml.Should().Contain("<Style Selector=\"ListBox.entity-list ListBoxItem:pointerover\">");
        xaml.Should().Contain("<Style Selector=\"ListBox.entity-list ListBoxItem:selected\">");
        xaml.Should().Contain("<Style Selector=\"ListBox.entity-list ListBoxItem:selected:pointerover\">");
        xaml.Should().Contain("<Style Selector=\"ListBox.entity-list ListBoxItem:focus-visible\">");
        xaml.Should().Contain("ListBox.entity-list ListBoxItem:pointerover /template/ ContentPresenter");
        xaml.Should().Contain("ListBox.entity-list ListBoxItem:selected /template/ ContentPresenter");
        xaml.Should().Contain("ListBox.entity-list ListBoxItem:selected:pointerover /template/ ContentPresenter");
        xaml.Should().Contain("ListBox.entity-list ListBoxItem:focus-visible /template/ ContentPresenter");
    }

    [Fact]
    public void EntityTreeViews_HighlightOnlySubscriptionRowsOnHover()
    {
        // Arrange
        var connectionTree = XDocument.Parse(File.ReadAllText(GetConnectionTreePath()));
        var azureTree = XDocument.Parse(File.ReadAllText(GetAzureTreePath()));
        var styles = File.ReadAllText(GetStylesPath());

        // Assert
        AssertSubscriptionButtonsUseHoverClass(connectionTree, "SelectSubscriptionForConnectionCommand");
        AssertSubscriptionButtonsUseHoverClass(azureTree, "SelectSubscriptionCommand");
        styles.Should().Contain("Expander.entity-tree-node /template/ ToggleButton#ExpanderHeader:pointerover /template/ Border#ToggleButtonBackground");
        styles.Should().Contain("Expander.entity-tree-node /template/ ToggleButton#ExpanderHeader:checked:pointerover /template/ Border#ToggleButtonBackground");
        styles.Should().Contain("Button.entity-tree-row:pointerover /template/ ContentPresenter");
        // Hover must not target the row surface Border: its Background is locally
        // bound to the selection converter, which always beats style setters.
        styles.Should().NotContain(":pointerover Border.entity-tree-row-surface");
        // The generic ListBoxItem hover must precede the entity-list transparent
        // overrides, otherwise it wins and highlights the whole item.
        styles.IndexOf("<Style Selector=\"ListBoxItem:pointerover /template/ ContentPresenter\">", StringComparison.Ordinal)
            .Should().BeLessThan(styles.IndexOf("<Style Selector=\"ListBox.entity-list ListBoxItem:pointerover\">", StringComparison.Ordinal));
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

    private static void AssertEntityGroupLoadingDoesNotBindToGlobalLoading(XDocument document)
    {
        document.Descendants()
            .Where(element => element.Attribute("Classes")?.Value == "entity-tree-loading")
            .Where(element => element.Attribute("IsVisible")?.Value == "{Binding IsLoading}")
            .Should()
            .BeEmpty();
    }

    private static void AssertSubscriptionButtonsUseHoverClass(XDocument document, string commandName)
    {
        var buttonsWithoutHoverClass = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(element => element.Attribute("Command")?.Value?.Contains(commandName, StringComparison.Ordinal) == true)
            .Where(element => element.Attribute("Classes")?.Value != "entity-tree-row subscription-tree-row")
            .ToList();

        buttonsWithoutHoverClass.Should().BeEmpty();
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

    private static IEnumerable<string> GetHeaderTooltips(XDocument document)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == "Grid" &&
                              element.Attribute("ColumnDefinitions")?.Value.StartsWith("Auto,*", StringComparison.Ordinal) == true)
            .First()
            .Elements()
            .Select(element => element.Attribute("ToolTip.Tip")?.Value)
            .Where(value => value is not null)
            .Select(value => value!);
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
