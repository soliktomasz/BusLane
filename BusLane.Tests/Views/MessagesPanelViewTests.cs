namespace BusLane.Tests.Views;

using System.Xml.Linq;

using FluentAssertions;

public class MessagesPanelViewTests
{
    [Fact]
    public void MessagesPanel_UsesInlineCommandBarSurface()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMessagesPanelPath());

        // Assert
        xaml.Should().Contain("Classes=\"message-command-bar\"");
        xaml.Should().Contain("Classes=\"message-search-surface\"");
    }

    [Fact]
    public void MessagesPanel_DoesNotUseCenteredLoadingCardCopy()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMessagesPanelPath());

        // Assert
        xaml.Should().NotContain("Please wait while we fetch the messages...");
        xaml.Should().Contain("Classes=\"inline-loading-surface\"");
    }

    [Fact]
    public void MessagesPanel_DisablesMessageListsWhileLoading()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMessagesPanelPath());

        // Act
        var disabledListCount = CountOccurrences(xaml, "IsEnabled=\"{Binding !CurrentMessageOps.IsLoadingMessages}\"");

        // Assert
        disabledListCount.Should().Be(2);
    }

    [Fact]
    public void MessagesPanel_DefinesOperatorEmptyStates()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMessagesPanelPath());

        // Assert
        xaml.Should().Contain("Choose an entity");
        xaml.Should().Contain("No active messages");
        xaml.Should().Contain("No dead-letter messages");
        xaml.Should().Contain("No messages match your search");
        CountOccurrences(xaml, "Classes=\"message-empty-state\"").Should().Be(6);
        CountOccurrences(xaml, "No active messages").Should().Be(1);
        CountOccurrences(xaml, "No dead-letter messages").Should().Be(1);
    }

    [Fact]
    public void MessagesPanel_UsesSingleNativeTabSelectionIndicator()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMessagesPanelPath());

        // Assert
        xaml.Should().NotContain("message-tab-underline");
    }

    [Fact]
    public void MessagesPanel_CollapsesSecondaryMessageActionsIntoOverflow()
    {
        // Arrange
        var document = XDocument.Parse(File.ReadAllText(GetMessagesPanelPath()));

        // Assert
        var overflowButton = document.Descendants()
            .Single(element => element.Name.LocalName == "Button" &&
                               element.Attribute("ToolTip.Tip")?.Value == "More message actions");

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
                "{Binding ToggleSortOrderCommand}",
                "{Binding CurrentMessageOps.ReceiveLockedMessagesCommand}",
                "{Binding PurgeMessagesCommand}"
            ]);

        var topActionTooltips = GetTopActionTooltips(document);

        topActionTooltips.Should().Contain([
            "Toggle multi-select mode (⌘M / Ctrl+M)",
            "Refresh messages",
            "More message actions"
        ]);
        topActionTooltips.Should().NotContain([
            "Toggle sort order",
            "Receive locked messages",
            "Purge messages"
        ]);
    }

    [Fact]
    public void MessagesPanel_SelectModeOffersFullMessageAndBodyOnlyExport()
    {
        // Arrange
        var document = XDocument.Parse(File.ReadAllText(GetMessagesPanelPath()));

        // Act
        var exportButton = document.Descendants()
            .Single(element => element.Name.LocalName == "Button" &&
                               element.Attribute("ToolTip.Tip")?.Value == "Export selected messages");

        var exportChoices = exportButton.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToList();

        // Assert
        exportButton.Attribute("IsEnabled")?.Value.Should().Be("{Binding CurrentMessageOps.HasSelectedMessages}");
        exportChoices.Select(element => element.Attribute("Command")?.Value).Should().Contain([
            "{Binding ExportSelectedMessagesCommand}",
            "{Binding ExportSelectedMessageBodiesCommand}"
        ]);
        exportChoices.SelectMany(element => element.Descendants())
            .Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => element.Attribute("Text")?.Value)
            .Should()
            .Contain(["Full message", "Body only"]);
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

    private static IEnumerable<string> GetTopActionTooltips(XDocument document)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == "StackPanel" &&
                              element.Attribute("Orientation")?.Value == "Horizontal" &&
                              element.Attribute("IsVisible")?.Value.Contains("CurrentNavigation.IsSessionInspectorTabSelected", StringComparison.Ordinal) == true)
            .First()
            .Elements()
            .Select(element => element.Attribute("ToolTip.Tip")?.Value)
            .Where(value => value is not null)
            .Select(value => value!);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
