namespace BusLane.Services.Monitoring;

using System.Text.Json;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class MetricsHistoryStore : IMetricsHistoryStore
{
    private static readonly TimeSpan CompactionInterval = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions LineJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly TimeSpan _retention;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly object _lock = new();
    private bool _isLoaded;
    private bool _requiresRewrite;
    private DateTimeOffset _lastCompactionAt;
    private List<MetricSnapshot> _snapshots = [];

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
        var snapshotList = snapshots as ICollection<MetricSnapshot> ?? snapshots.ToList();
        if (snapshotList.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            EnsureLoaded();
            _snapshots.AddRange(snapshotList);
            _snapshots = RemoveExpired(_snapshots);

            var now = _nowProvider();
            if (_requiresRewrite || now - _lastCompactionAt >= CompactionInterval)
            {
                SaveInternal(_snapshots);
                _requiresRewrite = false;
                _lastCompactionAt = now;
            }
            else
            {
                var cutoff = now - _retention;
                AppendInternal(snapshotList.Where(snapshot => snapshot.Timestamp >= cutoff));
            }
        }
    }

    public IReadOnlyList<MetricSnapshot> GetHistory(string entityName, string metricName, TimeSpan duration)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var cutoff = _nowProvider() - duration;
            return _snapshots
                .Where(s => s.EntityName == entityName && s.MetricName == metricName && s.Timestamp >= cutoff)
                .OrderBy(s => s.Timestamp)
                .ToList();
        }
    }

    public MetricWindowComparison CompareWindows(string entityName, string metricName, TimeSpan window)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var now = _nowProvider();
            var currentStart = now - window;
            var previousStart = currentStart - window;
            var all = _snapshots
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
            EnsureLoaded();
            _snapshots = RemoveExpired(_snapshots);
            SaveInternal(_snapshots);
            _requiresRewrite = false;
            _lastCompactionAt = _nowProvider();
        }
    }

    private void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        _snapshots = LoadInternal();
        _isLoaded = true;
    }

    private List<MetricSnapshot> RemoveExpired(List<MetricSnapshot> snapshots)
    {
        var cutoff = _nowProvider() - _retention;
        snapshots.RemoveAll(s => s.Timestamp < cutoff);
        return snapshots;
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
            if (json.AsSpan().TrimStart().StartsWith("["))
            {
                _requiresRewrite = true;
                return Deserialize<List<MetricSnapshot>>(json) ?? [];
            }

            var snapshots = new List<MetricSnapshot>();
            foreach (var line in File.ReadLines(_filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var snapshot = Deserialize<MetricSnapshot>(line);
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
                catch
                {
                    // Ignore incomplete final record left by interrupted append.
                }
            }

            return snapshots;
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

        var content = string.Join(
            Environment.NewLine,
            snapshots.Select(snapshot => Serialize(snapshot, LineJsonOptions)));
        if (content.Length > 0)
        {
            content += Environment.NewLine;
        }

        AtomicFile.WriteAllText(_filePath, content);
    }

    private void AppendInternal(IEnumerable<MetricSnapshot> snapshots)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = string.Join(
            Environment.NewLine,
            snapshots.Select(snapshot => Serialize(snapshot, LineJsonOptions)));
        if (content.Length > 0)
        {
            File.AppendAllText(_filePath, content + Environment.NewLine);
        }
    }
}
