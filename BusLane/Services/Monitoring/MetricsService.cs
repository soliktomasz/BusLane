namespace BusLane.Services.Monitoring;

using System.Collections.Concurrent;
using BusLane.Models;

/// <summary>
/// Thread-safe wrapper for a list of metric data points.
/// Ensures all operations on the underlying list are protected by a lock.
/// </summary>
internal sealed class MetricDataList : IDisposable
{
    private readonly List<MetricDataPoint> _list;
    private readonly System.Threading.ReaderWriterLockSlim _dataLock;
    private bool _disposed;

    public MetricDataList()
    {
        _list = new List<MetricDataPoint>();
        _dataLock = new System.Threading.ReaderWriterLockSlim();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dataLock.Dispose();
    }

    public void Add(MetricDataPoint dataPoint, int maxPoints)
    {
        _dataLock.EnterWriteLock();
        try
        {
            _list.Add(dataPoint);
            if (_list.Count > maxPoints)
            {
                _list.RemoveAt(0);
            }
        }
        finally
        {
            _dataLock.ExitWriteLock();
        }
    }

    public IReadOnlyList<MetricDataPoint> Where(System.Predicate<MetricDataPoint> predicate)
    {
        _dataLock.EnterReadLock();
        try
        {
            return _list.FindAll(predicate);
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    public IReadOnlyList<MetricDataPoint> ToList()
    {
        _dataLock.EnterReadLock();
        try
        {
            return _list.ToList();
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    public void RemoveAll(System.Predicate<MetricDataPoint> predicate)
    {
        _dataLock.EnterWriteLock();
        try
        {
            _list.RemoveAll(predicate);
        }
        finally
        {
            _dataLock.ExitWriteLock();
        }
    }

    public int Count
    {
        get
        {
            _dataLock.EnterReadLock();
            try
            {
                return _list.Count;
            }
            finally
            {
                _dataLock.ExitReadLock();
            }
        }
    }
}

public class MetricsService : IMetricsService, IDisposable
{
    internal const int MaxPointsPerMetric = 1000;
    private readonly ConcurrentDictionary<string, MetricDataList> _metrics = new();
    private bool _disposed;

    public event EventHandler<MetricDataPoint>? MetricRecorded;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _metrics)
        {
            kvp.Value.Dispose();
        }
        _metrics.Clear();
    }

    public void RecordMetric(string entityName, string metricName, double value)
    {
        var key = GetMetricKey(entityName, metricName);
        var dataPoint = new MetricDataPoint(DateTimeOffset.UtcNow, entityName, metricName, value);

        _metrics.AddOrUpdate(
            key,
            _ => {
                var list = new MetricDataList();
                list.Add(dataPoint, MaxPointsPerMetric);
                return list;
            },
            (_, list) => {
                list.Add(dataPoint, MaxPointsPerMetric);
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
            return list.Where(p => p.Timestamp >= cutoff);
        }

        return [];
    }

    public IEnumerable<MetricDataPoint> GetEntityMetrics(string entityName, TimeSpan duration)
    {
        var cutoff = DateTimeOffset.UtcNow - duration;
        var result = new List<MetricDataPoint>();
        var entityPrefix = $"{entityName}:";

        // Filter keys by entity prefix to avoid checking every metric list
        foreach (var kvp in _metrics.Where(k => k.Key.StartsWith(entityPrefix, StringComparison.Ordinal)))
        {
            result.AddRange(kvp.Value.Where(p => p.Timestamp >= cutoff));
        }

        return result.OrderBy(p => p.Timestamp);
    }

    public IEnumerable<MetricDataPoint> GetAggregatedMetrics(string metricName, TimeSpan duration)
    {
        var cutoff = DateTimeOffset.UtcNow - duration;
        var result = new List<MetricDataPoint>();
        var metricSuffix = $":{metricName}";

        // Filter keys by metric suffix
        foreach (var kvp in _metrics.Where(k => k.Key.EndsWith(metricSuffix, StringComparison.Ordinal)))
        {
            result.AddRange(kvp.Value.Where(p => p.Timestamp >= cutoff));
        }

        return result.OrderBy(p => p.Timestamp);
    }

    public void CleanupOldMetrics(TimeSpan retentionPeriod)
    {
        var cutoff = DateTimeOffset.UtcNow - retentionPeriod;

        foreach (var kvp in _metrics)
        {
            kvp.Value.RemoveAll(p => p.Timestamp < cutoff);
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

