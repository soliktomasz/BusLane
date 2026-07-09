namespace BusLane.Tests.Models;

using Azure.Messaging.ServiceBus;
using BusLane.Models;
using FluentAssertions;
using Xunit;

public class ReceivedMessageInfoTests
{
    [Fact]
    public void LockedUntil_UsesReceivedMessageTimestampForSettlementState()
    {
        // Arrange
        var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: "msg-1",
            body: BinaryData.FromString("body"),
            lockedUntil: DateTimeOffset.UtcNow.AddMinutes(5));
        var staleMessage = new MessageInfo(
            "msg-1",
            null,
            null,
            "body",
            DateTimeOffset.UtcNow,
            null,
            1,
            0,
            null,
            new Dictionary<string, object>(),
            LockedUntil: DateTimeOffset.UtcNow.AddMinutes(-5));
        var sut = new ReceivedMessageInfo(staleMessage, receivedMessage, "orders", null, false, false, null);

        // Act
        var lockedUntil = sut.LockedUntil;

        // Assert
        lockedUntil.Should().Be(receivedMessage.LockedUntil);
        sut.CanSettle.Should().BeTrue();
    }
}
