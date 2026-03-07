namespace BusLane.Services.Monitoring;

using System.Text.Json;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class MetricsHistoryStore : IMetricsHistoryStore
{
    private readonly string _filePath;
    private readonly TimeSpan _retention;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly object _lock = new();

    public MetricsHistoryStore(
        string? filePath = null,
        TimeSpan? retention = null,
        Func<DateTimeOffset>? nowProvider = null)
    {
        _filePath = filePath ?? AppPaths.MetricsHistory;
        _retention = retention ?? TimeSpan.FromDays(7);
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public void RecordSnapshots(IEnumerable<MetricSnapshot> snapshots)
    {
        lock (_lock)
        {
            var allSnapshots = LoadInternal();
            allSnapshots.AddRange(snapshots);
            SaveInternal(RemoveExpired(allSnapshots));
        }
    }

    public IReadOnlyList<MetricSnapshot> GetHistory(string entityName, string metricName, TimeSpan duration)
    {
        lock (_lock)
        {
            var cutoff = _nowProvider() - duration;
            return LoadInternal()
                .Where(s => s.EntityName == entityName && s.MetricName == metricName && s.Timestamp >= cutoff)
                .OrderBy(s => s.Timestamp)
                .ToList();
        }
    }

    public MetricWindowComparison CompareWindows(string entityName, string metricName, TimeSpan window)
    {
        lock (_lock)
        {
            var now = _nowProvider();
            var currentStart = now - window;
            var previousStart = currentStart - window;
            var all = LoadInternal()
                .Where(s => s.EntityName == entityName && s.MetricName == metricName)
                .ToList();

            var currentAverage = all
                .Where(s => s.Timestamp >= currentStart && s.Timestamp <= now)
                .Select(s => s.Value)
                .DefaultIfEmpty(0)
                .Average();

            var previousAverage = all
                .Where(s => s.Timestamp >= previousStart && s.Timestamp < currentStart)
                .Select(s => s.Value)
                .DefaultIfEmpty(0)
                .Average();

            return new MetricWindowComparison(entityName, metricName, window, currentAverage, previousAverage);
        }
    }

    public void CleanupExpiredSnapshots()
    {
        lock (_lock)
        {
            SaveInternal(RemoveExpired(LoadInternal()));
        }
    }

    private List<MetricSnapshot> RemoveExpired(List<MetricSnapshot> snapshots)
    {
        var cutoff = _nowProvider() - _retention;
        return snapshots
            .Where(s => s.Timestamp >= cutoff)
            .OrderBy(s => s.Timestamp)
            .ToList();
    }

    private List<MetricSnapshot> LoadInternal()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var json = File.ReadAllText(_filePath);
            return Deserialize<List<MetricSnapshot>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveInternal(List<MetricSnapshot> snapshots)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = Serialize(snapshots);
        File.WriteAllText(_filePath, json);
    }
}
