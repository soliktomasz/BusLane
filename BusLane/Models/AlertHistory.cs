namespace BusLane.Models;

/// <summary>
/// Quiet hours configuration using local-hour boundaries.
/// </summary>
public record QuietHoursWindow(int StartHour, int EndHour)
{
    public bool Contains(int hour)
    {
        if (StartHour == EndHour)
        {
            return true;
        }

        return StartHour < EndHour
            ? hour >= StartHour && hour < EndHour
            : hour >= StartHour || hour < EndHour;
    }
}

/// <summary>
/// Notification delivery channel types.
/// </summary>
public enum AlertDeliveryChannelType
{
    Webhook
}

/// <summary>
/// Delivery target attached to an alert rule.
/// </summary>
public record AlertDeliveryTarget(AlertDeliveryChannelType ChannelType, string Target);

/// <summary>
/// Why an alert evaluation did not create an active alert.
/// </summary>
public enum AlertSuppressionReason
{
    None,
    Cooldown,
    QuietHours
}

/// <summary>
/// Outcome of an alert evaluation for history.
/// </summary>
public enum AlertHistoryStatus
{
    Triggered,
    Suppressed,
    Delivered,
    DeliveryFailed,
    Acknowledged
}

/// <summary>
/// Historical alert entry for audit and troubleshooting.
/// </summary>
public record AlertHistoryEntry(
    string Id,
    string RuleId,
    string RuleName,
    string EntityName,
    string EntityType,
    double CurrentValue,
    DateTimeOffset Timestamp,
    AlertHistoryStatus Status,
    AlertSuppressionReason Reason = AlertSuppressionReason.None,
    string? Details = null);
