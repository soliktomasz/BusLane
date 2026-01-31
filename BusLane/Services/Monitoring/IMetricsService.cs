namespace BusLane.Services.Monitoring;

using BusLane.Models;

public interface IMetricsService : IDisposable
{
    /// <summary>
    /// Record a metric data point
    /// </summary>
    void RecordMetric(string entityName, string metricName, double value);
    
    /// <summary>
    /// Get metric history for an entity
    /// </summary>
    IEnumerable<MetricDataPoint> GetMetricHistory(string entityName, string metricName, TimeSpan duration);
    
    /// <summary>
    /// Get all metrics for an entity
    /// </summary>
    IEnumerable<MetricDataPoint> GetEntityMetrics(string entityName, TimeSpan duration);
    
    /// <summary>
    /// Get aggregated metrics across all entities
    /// </summary>
    IEnumerable<MetricDataPoint> GetAggregatedMetrics(string metricName, TimeSpan duration);
    
    /// <summary>
    /// Clear old metrics beyond retention period
    /// </summary>
    void CleanupOldMetrics(TimeSpan retentionPeriod);
    
    /// <summary>
    /// Event raised when new metrics are recorded
    /// </summary>
    event EventHandler<MetricDataPoint>? MetricRecorded;
    
    /// <summary>
    /// Event raised when a batch of metrics is recorded (debounced for performance)
    /// </summary>
    event EventHandler<IReadOnlyList<MetricDataPoint>>? MetricsBatchRecorded;
}

