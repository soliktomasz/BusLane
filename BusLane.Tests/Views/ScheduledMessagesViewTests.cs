namespace BusLane.Tests.Views;

using FluentAssertions;

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
}
