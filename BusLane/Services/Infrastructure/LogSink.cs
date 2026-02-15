namespace BusLane.Services.Infrastructure;

using BusLane.Models.Logging;

/// <summary>
/// In-memory log sink implementation with a bounded circular buffer.
/// </summary>
public sealed class LogSink : ILogSink
{
    private readonly int _maxEntries;
    private readonly object _lock = new();
    private readonly List<LogEntry> _entries;
    private int _index;

    public event Action<LogEntry>? OnLogAdded;

    public LogSink(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
        _entries = new List<LogEntry>(maxEntries);
    }

    public void Log(LogEntry entry)
    {
        lock (_lock)
        {
            // Add to circular buffer
            if (_entries.Count < _maxEntries)
            {
                _entries.Add(entry);
            }
            else
            {
                _entries[_index] = entry;
                _index = (_index + 1) % _maxEntries;
            }
        }

        // Fire event outside the lock to avoid blocking
        OnLogAdded?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> GetLogs()
    {
        lock (_lock)
        {
            // Sort only when reading - more efficient than sorting on every write
            return _entries
                .Where(e => e.Timestamp != default)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _index = 0;
        }
    }
}
