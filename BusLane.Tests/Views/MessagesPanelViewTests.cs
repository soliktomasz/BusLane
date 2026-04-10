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
}
