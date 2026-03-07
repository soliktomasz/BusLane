namespace BusLane.Services.Monitoring;

using BusLane.Models;

/// <summary>
/// Persists metric snapshots for historical comparisons.
/// </summary>
public interface IMetricsHistoryStore
{
    void RecordSnapshots(IEnumerable<MetricSnapshot> snapshots);

    IReadOnlyList<MetricSnapshot> GetHistory(string entityName, string metricName, TimeSpan duration);

    MetricWindowComparison CompareWindows(string entityName, string metricName, TimeSpan window);

    void CleanupExpiredSnapshots();
}
