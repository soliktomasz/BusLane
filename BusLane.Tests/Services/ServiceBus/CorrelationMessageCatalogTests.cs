namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;

public class CorrelationMessageCatalogTests
{
    [Fact]
    public void GetGroups_GroupsByCorrelationIdAndOrdersChronologically()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        sut.Add(CreateMessage("second", correlationId: "corr-1", enqueuedMinute: 2, sequenceNumber: 2));
        sut.Add(CreateMessage("first", correlationId: "corr-1", enqueuedMinute: 1, sequenceNumber: 1));

        // Act
        var group = sut.GetGroups().Single();

        // Assert
        group.Key.Should().Be("corr:corr-1");
        group.DisplayId.Should().Be("corr-1");
        group.UsesSessionFallback.Should().BeFalse();
        group.Messages.Select(static message => message.MessageId).Should().ContainInOrder("first", "second");
    }

    [Fact]
    public void GetGroups_WhenCorrelationIdIsMissing_UsesSessionId()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        sut.Add(CreateMessage("message-1", sessionId: "session-1"));

        // Act
        var group = sut.GetGroups().Single();

        // Assert
        group.Key.Should().Be("session:session-1");
        group.DisplayId.Should().Be("session-1");
        group.UsesSessionFallback.Should().BeTrue();
    }

    [Fact]
    public void GetGroups_WhenBothIdentifiersAreMissing_OmitsMessage()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        sut.Add(CreateMessage("message-1"));

        // Act
        var groups = sut.GetGroups();

        // Assert
        groups.Should().BeEmpty();
    }

    [Fact]
    public void Add_WhenSameMessageIsObservedTwice_DeduplicatesIt()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        var message = CreateMessage("message-1", correlationId: "corr-1");

        // Act
        sut.Add(message);
        sut.Add(message with { Body = "updated observation" });

        // Assert
        sut.GetGroups().Single().Messages.Should().ContainSingle()
            .Which.Body.Should().Be("updated observation");
    }

    [Fact]
    public void Add_WhenCapacityIsExceeded_EvictsOldestObservation()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog(capacity: 2);

        // Act
        sut.Add(CreateMessage("oldest", correlationId: "corr-1", enqueuedMinute: 1, sequenceNumber: 1));
        sut.Add(CreateMessage("middle", correlationId: "corr-1", enqueuedMinute: 2, sequenceNumber: 2));
        sut.Add(CreateMessage("newest", correlationId: "corr-1", enqueuedMinute: 3, sequenceNumber: 3));

        // Assert
        sut.GetGroups().Single().Messages.Select(static message => message.MessageId)
            .Should().ContainInOrder("middle", "newest");
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        sut.Add(CreateMessage("message-1", correlationId: "corr-1"));

        // Act
        sut.Clear();

        // Assert
        sut.GetGroups().Should().BeEmpty();
    }

    private static CorrelationMessage CreateMessage(
        string messageId,
        string? correlationId = null,
        string? sessionId = null,
        int enqueuedMinute = 1,
        long sequenceNumber = 1)
    {
        return new CorrelationMessage(
            CorrelationMessageSource.Loaded,
            "namespace.servicebus.windows.net",
            ConnectionEnvironment.Development,
            "orders",
            "Queue",
            TopicName: null,
            SubscriptionName: null,
            messageId,
            correlationId,
            sessionId,
            "application/json",
            "{}",
            new DateTimeOffset(2026, 7, 28, 10, enqueuedMinute, 0, TimeSpan.Zero),
            sequenceNumber,
            new Dictionary<string, object>());
    }
}
