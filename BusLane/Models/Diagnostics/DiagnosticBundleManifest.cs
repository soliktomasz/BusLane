namespace BusLane.Models.Diagnostics;

using BusLane.Models.Logging;

/// <summary>
/// Manifest for a diagnostic bundle export.
/// </summary>
public record DiagnosticBundleManifest(
    string AppVersion,
    DateTimeOffset CreatedAt,
    string OperatingSystem,
    string RuntimeVersion,
    IReadOnlyDictionary<string, object?> Preferences,
    IReadOnlyList<object> Connections,
    IReadOnlyList<AlertHistoryEntry> AlertHistory,
    IReadOnlyList<LogEntry> Logs);
