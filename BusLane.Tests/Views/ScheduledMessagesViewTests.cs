namespace BusLane.Tests.Views;

using FluentAssertions;
using System.Xml.Linq;

public class ScheduledMessagesViewTests
{
    [Fact]
    public void ScheduledMessagesView_ContainsManagementConsoleContracts()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "BusLane",
            "Views", "Controls", "ScheduledMessagesView.axaml"));

        File.Exists(path).Should().BeTrue();
        var xaml = File.ReadAllText(path);
        foreach (var value in new[]
                 {
                     "SearchText", "SelectedConnection", "SelectedEntity", "SelectedEnvironment",
                     "SelectedStatus", "SelectedTimeRange", "ShowListCommand", "ShowCalendarCommand",
                     "PreviousMonthCommand", "NextMonthCommand", "CloneCommand", "BeginCancelCommand",
                     "BeginRescheduleCommand", "RefreshCommand", "ResolveCommand",
                     "local index", "broker confirmed", "IsProductionAcknowledged",
                     "partial failure", "No scheduled messages"
                 })
        {
            xaml.Should().Contain(value);
        }

        foreach (var value in new[]
                 {
                     "Filters", "Connection filter", "Entity filter", "Environment filter",
                     "Status filter", "Time range filter", "FilteredEntries.Count",
                     "Scheduled", "Message / status", "Actions",
                     "No scheduled messages match these filters",
                     "IsVisible=\"{Binding !IsEmpty}\""
                 })
        {
            xaml.Should().Contain(value);
        }
    }

    [Fact]
    public void ScheduledMessagesView_CollapsesRowActionsIntoFlyout()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "BusLane",
            "Views", "Controls", "ScheduledMessagesView.axaml"));
        var document = XDocument.Parse(File.ReadAllText(path));

        var actionButton = document.Descendants()
            .Single(element => element.Name.LocalName == "Button" &&
                               element.Attribute("ToolTip.Tip")?.Value == "More scheduled message actions");

        actionButton.Descendants()
            .Single(element => element.Name.LocalName == "Flyout")
            .Attribute("Placement")
            ?.Value
            .Should()
            .Be("BottomEdgeAlignedRight");

        actionButton.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Command")?.Value)
            .Should()
            .Contain([
                "{Binding $parent[UserControl].DataContext.CloneCommand}",
                "{Binding $parent[UserControl].DataContext.BeginCancelCommand}",
                "{Binding $parent[UserControl].DataContext.BeginRescheduleCommand}",
                "{Binding $parent[UserControl].DataContext.ResolveCommand}"
            ]);

        document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Content")?.Value)
            .Should()
            .NotContain(["Clone", "Cancel", "Reschedule", "Resolve"]);
    }
}
