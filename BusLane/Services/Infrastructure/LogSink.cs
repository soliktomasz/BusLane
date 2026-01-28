using BusLane.Models.Logging;

namespace BusLane.Services.Infrastructure;

/// <summary>
/// In-memory log sink implementation with a bounded circular buffer.
/// </summary>
public sealed class LogSink : ILogSink
{
    private readonly int _maxEntries;
    private readonly object _lock = new();
    private readonly List<LogEntry> _entries;
    private readonly List<LogEntry> _filteredEntries; // For thread-safe reads
    private int _index;

    public event Action<LogEntry>? OnLogAdded;

    public LogSink(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
        _entries = new List<LogEntry>(maxEntries);
        _filteredEntries = new List<LogEntry>();
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

            // Update filtered list for thread-safe reads
            _filteredEntries.Clear();
            _filteredEntries.AddRange(_entries.Where(e => e.Timestamp != default).OrderByDescending(e => e.Timestamp));
        }

        // Fire event outside the lock to avoid blocking
        OnLogAdded?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> GetLogs()
    {
        lock (_lock)
        {
            return _filteredEntries.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _filteredEntries.Clear();
            _index = 0;
        }
    }
}
