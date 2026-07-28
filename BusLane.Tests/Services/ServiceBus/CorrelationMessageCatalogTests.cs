namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;

public class CorrelationMessageCatalogTests
{
    [Fact]
    public void CorrelationMessageIdentity_FromMessage_UsesStableTransportIdentity()
    {
        // Arrange
        var message = CreateMessage("message-1", correlationId: "corr-1", sequenceNumber: 42);

        // Act
        var first = CorrelationMessageIdentity.From(message);
        var second = CorrelationMessageIdentity.From(message with { Body = "updated" });

        // Assert
        first.Should().Be(second);
    }

    [Fact]
    public void Add_AfterMutation_RaisesChangedWithAffectedGroup()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        CorrelationCatalogChangedEventArgs? observed = null;
        sut.Changed += (_, args) =>
        {
            observed = args;
            Task.Run(sut.GetGroups).Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        };

        // Act
        sut.Add(CreateMessage("message-1", correlationId: "corr-1"));

        // Assert
        observed.Should().NotBeNull();
        observed!.AffectedGroupKeys.Should().ContainSingle("corr:corr-1");
        observed.ChangeKind.Should().Be(CorrelationCatalogChangeKind.Added);
    }

    [Fact]
    public void Add_WhenReplacingMessage_RaisesReplaced()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        var message = CreateMessage("message-1", correlationId: "corr-1");
        sut.Add(message);
        CorrelationCatalogChangedEventArgs? observed = null;
        sut.Changed += (_, args) => observed = args;

        // Act
        sut.Add(message with { Body = "updated" });

        // Assert
        observed!.ChangeKind.Should().Be(CorrelationCatalogChangeKind.Replaced);
        observed.AffectedGroupKeys.Should().ContainSingle("corr:corr-1");
    }

    [Fact]
    public void Add_WhenEvictingMessage_ReportsAddedAndEvictedGroups()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog(capacity: 1);
        sut.Add(CreateMessage("old", correlationId: "corr-old"));
        CorrelationCatalogChangedEventArgs? observed = null;
        sut.Changed += (_, args) => observed = args;

        // Act
        sut.Add(CreateMessage("new", correlationId: "corr-new", sequenceNumber: 2));

        // Assert
        observed!.ChangeKind.Should().Be(CorrelationCatalogChangeKind.Evicted);
        observed.AffectedGroupKeys.Should().BeEquivalentTo("corr:corr-old", "corr:corr-new");
    }

    [Fact]
    public void AddRange_RaisesSingleCoalescedNotification()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        var notifications = new List<CorrelationCatalogChangedEventArgs>();
        sut.Changed += (_, args) => notifications.Add(args);

        // Act
        sut.AddRange([
            CreateMessage("first", correlationId: "corr-1"),
            CreateMessage("second", sessionId: "session-1", sequenceNumber: 2)
        ]);

        // Assert
        notifications.Should().ContainSingle();
        notifications[0].ChangeKind.Should().Be(CorrelationCatalogChangeKind.RangeAdded);
        notifications[0].AffectedGroupKeys.Should().BeEquivalentTo("corr:corr-1", "session:session-1");
    }

    [Fact]
    public void AddRange_WhenMessagesAreNotGroupable_DoesNotRaiseChanged()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        var notificationCount = 0;
        sut.Changed += (_, _) => notificationCount++;

        // Act
        sut.AddRange([CreateMessage("message-1")]);

        // Assert
        notificationCount.Should().Be(0);
    }

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

    [Fact]
    public void Clear_WhenCatalogHasGroupableMessages_RaisesCleared()
    {
        // Arrange
        var sut = new CorrelationMessageCatalog();
        sut.Add(CreateMessage("message-1", correlationId: "corr-1"));
        CorrelationCatalogChangedEventArgs? observed = null;
        sut.Changed += (_, args) => observed = args;

        // Act
        sut.Clear();

        // Assert
        observed!.ChangeKind.Should().Be(CorrelationCatalogChangeKind.Cleared);
        observed.AffectedGroupKeys.Should().ContainSingle("corr:corr-1");
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
