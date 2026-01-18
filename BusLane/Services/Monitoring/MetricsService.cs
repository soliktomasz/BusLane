namespace BusLane.Services.Monitoring;

using System.Collections.Concurrent;
using BusLane.Models;

public class MetricsService : IMetricsService
{
    private readonly ConcurrentDictionary<string, List<MetricDataPoint>> _metrics = new();
    private readonly System.Threading.ReaderWriterLockSlim _lock = new();
    private const int MaxPointsPerMetric = 1000;

    public event EventHandler<MetricDataPoint>? MetricRecorded;

    public void RecordMetric(string entityName, string metricName, double value)
    {
        var key = GetMetricKey(entityName, metricName);
        var dataPoint = new MetricDataPoint(DateTimeOffset.UtcNow, entityName, metricName, value);

        _metrics.AddOrUpdate(
            key,
            _ => new List<MetricDataPoint> { dataPoint },
            (_, list) =>
            {
                _lock.EnterWriteLock();
                try
                {
                    list.Add(dataPoint);
                    if (list.Count > MaxPointsPerMetric)
                    {
                        list.RemoveAt(0);
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
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
            _lock.EnterReadLock();
            try
            {
                return list.Where(p => p.Timestamp >= cutoff).ToList();
            }
            finally
            {
                _lock.ExitReadLock();
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
            _lock.EnterReadLock();
            try
            {
                result.AddRange(kvp.Value.Where(p => p.EntityName == entityName && p.Timestamp >= cutoff));
            }
            finally
            {
                _lock.ExitReadLock();
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
            _lock.EnterReadLock();
            try
            {
                result.AddRange(kvp.Value.Where(p => p.Timestamp >= cutoff));
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        return result.OrderBy(p => p.Timestamp);
    }

    public void CleanupOldMetrics(TimeSpan retentionPeriod)
    {
        var cutoff = DateTimeOffset.UtcNow - retentionPeriod;

        foreach (var kvp in _metrics)
        {
            _lock.EnterWriteLock();
            try
            {
                kvp.Value.RemoveAll(p => p.Timestamp < cutoff);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

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

