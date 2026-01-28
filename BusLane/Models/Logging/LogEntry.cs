namespace BusLane.Models.Logging;

/// <summary>
/// Represents the source of a log entry.
/// </summary>
public enum LogSource
{
    Application,
    ServiceBus
}

/// <summary>
/// Represents the severity level of a log entry.
/// </summary>
public enum LogLevel
{
    Info,
    Warning,
    Error,
    Debug
}

/// <summary>
/// Represents a single log entry in the application.
/// </summary>
/// <param name="Timestamp">When the log entry was created.</param>
/// <param name="Source">The source of the log entry (Application or ServiceBus).</param>
/// <param name="Level">The severity level of the log entry.</param>
/// <param name="Message">The main log message.</param>
/// <param name="Details">Optional detailed information for expanded view.</param>
public record LogEntry(
    DateTime Timestamp,
    LogSource Source,
    LogLevel Level,
    string Message,
    string? Details = null
);
