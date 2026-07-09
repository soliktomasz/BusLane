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
    internal SessionReceiverLease? SessionReceiverLease { get; init; }

    public string? LockToken => Message.LockToken;
    public DateTimeOffset? RenewedLockedUntil { get; init; }
    public DateTimeOffset? LockedUntil => RenewedLockedUntil ?? ReceivedMessage.LockedUntil;
    public bool CanSettle => LockedUntil > DateTimeOffset.UtcNow;

    public ReceivedMessageInfo WithLockedUntil(DateTimeOffset lockedUntil) =>
        this with
        {
            Message = Message with { LockedUntil = lockedUntil },
            RenewedLockedUntil = lockedUntil
        };
}

internal sealed class SessionReceiverLease(ServiceBusReceiver receiver, int settlementCount) : IAsyncDisposable
{
    private int _remainingSettlements = Math.Max(1, settlementCount);
    private int _disposed;

    public ServiceBusReceiver Receiver { get; } = receiver;

    public async ValueTask ReleaseSettlementAsync()
    {
        if (Interlocked.Decrement(ref _remainingSettlements) <= 0)
        {
            await DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await Receiver.DisposeAsync();
        }
    }
}
