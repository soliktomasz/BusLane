namespace BusLane.Models.Logging;

/// <summary>
/// Sink interface for logging entries.
/// Services emit logs through this interface, and the UI subscribes to them.
/// </summary>
public interface ILogSink
{
    /// <summary>
    /// Adds a new log entry.
    /// </summary>
    void Log(LogEntry entry);

    /// <summary>
    /// Gets all log entries.
    /// </summary>
    IReadOnlyList<LogEntry> GetLogs();

    /// <summary>
    /// Clears all log entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Event fired when a new log entry is added.
    /// </summary>
    event Action<LogEntry>? OnLogAdded;
}
