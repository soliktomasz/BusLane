namespace BusLane.Tests.Views;

using FluentAssertions;
using System.Xml.Linq;

public class ScheduledMessagesViewTests
{
    [Fact]
    public void ScheduledMessagesView_ContainsManagementConsoleContracts()
    {
        var xaml = File.ReadAllText(GetViewPath());

        foreach (var value in new[]
                 {
                     "SearchText", "SelectedConnection", "SelectedEntity", "SelectedEnvironment",
                     "SelectedStatus", "SelectedTimeRange", "ShowListCommand", "ShowCalendarCommand",
                     "PreviousMonthCommand", "NextMonthCommand", "CloneCommand", "BeginCancelCommand",
                     "BeginRescheduleCommand", "ResolveCommand", "ClearFiltersCommand",
                     "Local index", "Broker confirmed", "IsProductionAcknowledged",
                     "the original stays cancelled", "No scheduled messages yet",
                     "Connection filter", "Entity filter", "Environment filter",
                     "Status filter", "Time range filter", "FilteredEntries.Count",
                     "Scheduled for", "Destination", "State",
                     "No messages match these filters",
                     "IsVisible=\"{Binding !IsEmpty}\""
                 })
        {
            xaml.Should().Contain(value);
        }
    }

    [Fact]
    public void ScheduledMessagesView_KeepsRescheduleVisibleAndOtherActionsInFlyout()
    {
        var document = XDocument.Parse(File.ReadAllText(GetViewPath()));

        var moreButton = document.Descendants()
            .Single(element => element.Name.LocalName == "Button" &&
                               element.Attribute("AutomationProperties.Name")?.Value == "More scheduled message actions");

        moreButton.Descendants()
            .Single(element => element.Name.LocalName == "Flyout")
            .Attribute("Placement")?.Value
            .Should().Be("BottomEdgeAlignedRight");

        moreButton.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Command")?.Value)
            .Should()
            .Contain([
                "{Binding $parent[UserControl].DataContext.CloneCommand}",
                "{Binding $parent[UserControl].DataContext.BeginCancelCommand}",
                "{Binding $parent[UserControl].DataContext.ResolveCommand}"
            ]);

        moreButton.Parent!.Elements()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Command")?.Value)
            .Should()
            .Contain("{Binding $parent[UserControl].DataContext.BeginRescheduleCommand}");
    }

    [Fact]
    public void ScheduledMessagesView_ShowsLocalStateAsLabelledPill()
    {
        var xaml = File.ReadAllText(GetViewPath());

        xaml.Should().Contain("{Binding LocalState, Converter={x:Static conv:ScheduledMessageConverters.StateLabel}}");
        xaml.Should().Contain("ScheduledStateToBrushConverter");
    }

    private static string GetViewPath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "BusLane",
        "Views", "Controls", "ScheduledMessagesView.axaml"));
}
