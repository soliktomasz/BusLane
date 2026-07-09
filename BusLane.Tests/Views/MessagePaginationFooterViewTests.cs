namespace BusLane.Tests.Views;

using System.Xml.Linq;

using FluentAssertions;

public class MessagePaginationFooterViewTests
{
    [Fact]
    public void PaginationFooter_GroupsAccessibleNavigationControls()
    {
        // Arrange
        var document = XDocument.Parse(File.ReadAllText(GetPaginationFooterPath()));

        // Act
        var rangeSummary = document.Descendants()
            .Single(element => element.Name.LocalName == "StackPanel" &&
                               element.Attribute("Classes")?.Value == "pager-range-summary");
        var navigationGroup = document.Descendants()
            .Single(element => element.Name.LocalName == "Border" &&
                               element.Attribute("Classes")?.Value == "pager-navigation-group");
        var navigationItems = navigationGroup.Elements()
            .Single(element => element.Name.LocalName == "StackPanel" &&
                               element.Attribute("Orientation")?.Value == "Horizontal")
            .Elements()
            .ToList();
        var navigationGroupStyle = document.Descendants()
            .Single(element => element.Name.LocalName == "Style" &&
                               element.Attribute("Selector")?.Value == "Border.pager-navigation-group");
        var previousButtonIndex = navigationItems.FindIndex(element =>
            element.Name.LocalName == "Button" &&
            element.Attribute("Command")?.Value == "{Binding LoadPreviousPageCommand}");
        var pageBadgeIndex = navigationItems.FindIndex(element =>
            element.Name.LocalName == "Border" &&
            element.Attribute("Classes")?.Value == "pager-badge");
        var nextButtonIndex = navigationItems.FindIndex(element =>
            element.Name.LocalName == "Button" &&
            element.Attribute("Command")?.Value == "{Binding LoadNextPageCommand}");

        // Assert
        rangeSummary.Attribute("Grid.Column")?.Value.Should().Be("0");
        rangeSummary.Descendants().Select(element => element.Attribute("Text")?.Value).Should().Contain([
            "{Binding Pagination.PageRangeText}",
            "{Binding Pagination.PageDetailText}"
        ]);

        navigationGroup.Attribute("Grid.Column")?.Value.Should().Be("1");
        navigationGroupStyle.Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Select(element => element.Attribute("Property")?.Value)
            .Should()
            .Contain(["Background", "BorderBrush", "BorderThickness", "CornerRadius", "Padding"]);
        navigationGroup.Descendants()
            .Single(element => element.Name.LocalName == "Button" &&
                               element.Attribute("Command")?.Value == "{Binding LoadPreviousPageCommand}")
            .Attribute("AutomationProperties.Name")?.Value
            .Should()
            .Be("Previous page");
        navigationGroup.Descendants()
            .Single(element => element.Name.LocalName == "Button" &&
                               element.Attribute("Command")?.Value == "{Binding LoadNextPageCommand}")
            .Attribute("AutomationProperties.Name")?.Value
            .Should()
            .Be("Next page");
        navigationGroup.Descendants()
            .Single(element => element.Name.LocalName == "TextBlock" &&
                               element.Attribute("Text")?.Value == "{Binding Pagination.PageLabel}");

        previousButtonIndex.Should().BeGreaterThanOrEqualTo(0);
        pageBadgeIndex.Should().BeGreaterThan(previousButtonIndex);
        nextButtonIndex.Should().BeGreaterThan(pageBadgeIndex);
    }

    private static string GetPaginationFooterPath()
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
            "MessagePaginationFooterView.axaml"));
    }
}
