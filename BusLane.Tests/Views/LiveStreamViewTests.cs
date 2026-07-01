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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var projectPath = Path.Combine(directory.FullName, "BusLane", "BusLane.csproj");
            if (File.Exists(projectPath))
            {
                return Path.Combine(directory.FullName, "BusLane", "Views", "Controls", "LiveStreamView.axaml");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BusLane project root.");
    }
}
