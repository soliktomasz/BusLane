namespace BusLane.Tests.Views;

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
