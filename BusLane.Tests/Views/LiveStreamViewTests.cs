namespace BusLane.Tests.Views;

using FluentAssertions;

public class LiveStreamViewTests
{
    [Fact]
    public void LiveStreamMessageList_UsesVirtualizingStackPanel()
    {
        // Arrange
        var xaml = File.ReadAllText(GetLiveStreamViewPath());
        var listStart = xaml.IndexOf("ItemsSource=\"{Binding FilteredMessages}\"", StringComparison.Ordinal);

        // Act
        var listEnd = xaml.IndexOf("</ListBox>", listStart, StringComparison.Ordinal);
        var listXaml = xaml[listStart..listEnd];

        // Assert
        listXaml.Should().Contain("<VirtualizingStackPanel/>");
    }

    private static string GetLiveStreamViewPath()
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
            "LiveStreamView.axaml"));
    }
}
