namespace BusLane.Tests.Views;

using System.Xml.Linq;

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

    [Fact]
    public void LiveStreamView_StaticConverterReference_ResolvesFromLocalResources()
    {
        // Arrange
        var document = XDocument.Parse(File.ReadAllText(GetLiveStreamViewPath()));
        var xamlNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        // Act
        var resourceKeys = document
            .Descendants()
            .Where(element => element.Parent?.Name.LocalName == "UserControl.Resources")
            .Select(element => element.Attribute(xamlNamespace + "Key")?.Value)
            .Where(key => key != null)
            .ToList();

        // Assert
        resourceKeys.Should().Contain("IntEqualsConverter");
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
