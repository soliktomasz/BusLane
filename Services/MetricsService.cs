using System.Collections.Concurrent;
using BusLane.Models;

namespace BusLane.Services;

public class MetricsService : IMetricsService
{
    private readonly ConcurrentDictionary<string, List<MetricDataPoint>> _metrics = new();
    private readonly object _lock = new();
    private const int MaxPointsPerMetric = 1000;

    public event EventHandler<MetricDataPoint>? MetricRecorded;

    public void RecordMetric(string entityName, string metricName, double value)
    {
        var key = GetMetricKey(entityName, metricName);
        var dataPoint = new MetricDataPoint(DateTimeOffset.UtcNow, entityName, metricName, value);

        _metrics.AddOrUpdate(
            key,
            _ => [dataPoint],
            (_, list) =>
            {
                lock (_lock)
                {
                    list.Add(dataPoint);
                    // Keep only last N points
                    if (list.Count > MaxPointsPerMetric)
                    {
                        list.RemoveAt(0);
                    }
                }
                return list;
            }
        );

        MetricRecorded?.Invoke(this, dataPoint);
    }

    public IEnumerable<MetricDataPoint> GetMetricHistory(string entityName, string metricName, TimeSpan duration)
    {
        var key = GetMetricKey(entityName, metricName);
        var cutoff = DateTimeOffset.UtcNow - duration;

        if (_metrics.TryGetValue(key, out var list))
        {
            lock (_lock)
            {
                return list.Where(p => p.Timestamp >= cutoff).ToList();
            }
        }

        return [];
    }

    public IEnumerable<MetricDataPoint> GetEntityMetrics(string entityName, TimeSpan duration)
    {
        var cutoff = DateTimeOffset.UtcNow - duration;
        var result = new List<MetricDataPoint>();

        foreach (var kvp in _metrics)
        {
            lock (_lock)
            {
                result.AddRange(kvp.Value.Where(p => p.EntityName == entityName && p.Timestamp >= cutoff));
            }
        }

        return result.OrderBy(p => p.Timestamp);
    }

    public IEnumerable<MetricDataPoint> GetAggregatedMetrics(string metricName, TimeSpan duration)
    {
        var cutoff = DateTimeOffset.UtcNow - duration;
        var result = new List<MetricDataPoint>();

        foreach (var kvp in _metrics.Where(k => k.Key.EndsWith($":{metricName}")))
        {
            lock (_lock)
            {
                result.AddRange(kvp.Value.Where(p => p.Timestamp >= cutoff));
            }
        }

        return result.OrderBy(p => p.Timestamp);
    }

    public void CleanupOldMetrics(TimeSpan retentionPeriod)
    {
        var cutoff = DateTimeOffset.UtcNow - retentionPeriod;

        foreach (var kvp in _metrics)
        {
            lock (_lock)
            {
                kvp.Value.RemoveAll(p => p.Timestamp < cutoff);
            }
        }

        // Remove empty entries
        var emptyKeys = _metrics.Where(kvp => kvp.Value.Count == 0).Select(kvp => kvp.Key).ToList();
        foreach (var key in emptyKeys)
        {
            _metrics.TryRemove(key, out _);
        }
    }

    private static string GetMetricKey(string entityName, string metricName)
    {
        return $"{entityName}:{metricName}";
    }
}

