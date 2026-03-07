namespace BusLane.Services.Monitoring;

using BusLane.Models;

/// <summary>
/// Sends alert notifications to external targets.
/// </summary>
public interface INotificationChannel
{
    AlertDeliveryChannelType ChannelType { get; }

    Task<AlertHistoryEntry> SendAsync(
        AlertEvent alert,
        AlertDeliveryTarget target,
        CancellationToken ct = default);
}
