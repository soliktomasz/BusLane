namespace BusLane.Models;

public enum AlertType
{
    DeadLetterThreshold,
    MessageCountThreshold,
    QueueSizeThreshold,
    InactivityThreshold
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public record AlertRule(
    string Id,
    string Name,
    AlertType Type,
    AlertSeverity Severity,
    double Threshold,
    bool IsEnabled = true,
    string? EntityPattern = null // null = all entities, or regex pattern
);

public record AlertEvent(
    string Id,
    AlertRule Rule,
    string EntityName,
    string EntityType, // "Queue", "Topic", "Subscription"
    double CurrentValue,
    DateTimeOffset TriggeredAt,
    bool IsAcknowledged = false
);

public record MetricDataPoint(
    DateTimeOffset Timestamp,
    string EntityName,
    string MetricName,
    double Value
);

