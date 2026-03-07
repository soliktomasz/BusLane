namespace BusLane.Models;

/// <summary>
/// Explicit health state for a live Service Bus connection.
/// </summary>
public enum ConnectionHealthState
{
    Healthy,
    Degraded,
    AuthExpired,
    NetworkFailed,
    Throttled
}

/// <summary>
/// Summary of connection health and the most likely remediation.
/// </summary>
public record ConnectionHealthReport(
    ConnectionHealthState State,
    string Summary,
    string? RecommendedAction = null);
