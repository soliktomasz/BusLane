namespace BusLane.Models;

using Azure.Messaging.ServiceBus;

/// <summary>
/// Message received in peek-lock mode and still eligible for settlement while its lock is valid.
/// </summary>
public record ReceivedMessageInfo(
    MessageInfo Message,
    ServiceBusReceivedMessage ReceivedMessage,
    string EntityName,
    string? SubscriptionName,
    bool DeadLetter,
    bool RequiresSession,
    string? SessionId)
{
    public string? LockToken => Message.LockToken;
    public DateTimeOffset? LockedUntil => Message.LockedUntil;
    public bool CanSettle => ReceivedMessage.LockedUntil > DateTimeOffset.UtcNow;
}
