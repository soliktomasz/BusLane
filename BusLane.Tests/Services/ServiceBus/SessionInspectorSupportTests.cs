namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;

public class SessionInspectorSupportTests
{
    [Fact]
    public void BuildSessionInspectorItem_MergesActiveAndDeadLetterSnapshots()
    {
        // Arrange
        var active = new ServiceBusOperations.SessionMessageSnapshot(
            "session-a",
            MessageCount: 4,
            LastActivityAt: DateTimeOffset.Parse("2026-03-07T10:15:00Z"),
            LockedUntil: DateTimeOffset.Parse("2026-03-07T10:20:00Z"),
            State: "active-state");
        var deadLetter = new ServiceBusOperations.SessionMessageSnapshot(
            "session-a",
            MessageCount: 2,
            LastActivityAt: DateTimeOffset.Parse("2026-03-07T10:25:00Z"),
            LockedUntil: null,
            State: null);

        // Act
        SessionInspectorItem item = ServiceBusOperations.BuildSessionInspectorItem(active, deadLetter);

        // Assert
        item.SessionId.Should().Be("session-a");
        item.ActiveMessageCount.Should().Be(4);
        item.DeadLetterMessageCount.Should().Be(2);
        item.TotalMessageCount.Should().Be(6);
        item.HasDeadLetter.Should().BeTrue();
        item.LastActivityAt.Should().Be(DateTimeOffset.Parse("2026-03-07T10:25:00Z"));
        item.LockedUntil.Should().Be(DateTimeOffset.Parse("2026-03-07T10:20:00Z"));
        item.State.Should().Be("active-state");
    }
}
