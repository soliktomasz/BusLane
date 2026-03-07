namespace BusLane.Models;

/// <summary>
/// Persisted metric snapshot.
/// </summary>
public record MetricSnapshot(
    DateTimeOffset Timestamp,
    string EntityName,
    string MetricName,
    double Value);

/// <summary>
/// Comparison between the current and previous metric windows.
/// </summary>
public record MetricWindowComparison(
    string EntityName,
    string MetricName,
    TimeSpan Window,
    double CurrentAverage,
    double PreviousAverage)
{
    public double Delta => CurrentAverage - PreviousAverage;
}
