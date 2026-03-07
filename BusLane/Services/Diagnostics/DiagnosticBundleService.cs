namespace BusLane.Services.Diagnostics;

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using BusLane.Models;
using BusLane.Models.Diagnostics;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.Dashboard;
using BusLane.Services.Infrastructure;
using BusLane.Services.Monitoring;
using BusLane.Services.Storage;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class DiagnosticBundleService : IDiagnosticBundleService
{
    private static readonly Regex SharedAccessKeyPattern = new(
        @"SharedAccessKey=[^;\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndpointPattern = new(
        @"Endpoint=sb://[^;/\s]+/?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ServiceBusHostPattern = new(
        @"[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.servicebus\.windows\.net",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogSink _logSink;
    private readonly IPreferencesService _preferencesService;
    private readonly IAlertService _alertService;
    private readonly IDashboardPersistenceService _dashboardPersistenceService;
    private readonly IConnectionStorageService _connectionStorageService;
    private readonly IVersionService _versionService;
    private readonly string _bundleDirectory;

    public DiagnosticBundleService(
        ILogSink logSink,
        IPreferencesService preferencesService,
        IAlertService alertService,
        IDashboardPersistenceService dashboardPersistenceService,
        IConnectionStorageService connectionStorageService,
        IVersionService versionService,
        string? bundleDirectory = null)
    {
        _logSink = logSink;
        _preferencesService = preferencesService;
        _alertService = alertService;
        _dashboardPersistenceService = dashboardPersistenceService;
        _connectionStorageService = connectionStorageService;
        _versionService = versionService;
        _bundleDirectory = bundleDirectory ?? AppPaths.DiagnosticBundles;
    }

    public async Task<string> ExportAsync(bool includeMessageBodies = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_bundleDirectory);
        var bundlePath = Path.Combine(_bundleDirectory, $"buslane-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        var preferencesSnapshot = BuildPreferencesSnapshot();
        var connectionsSnapshot = await BuildConnectionsSnapshotAsync(ct);
        var alertsHistory = _alertService.History;
        var logSnapshot = BuildLogSnapshot();

        var manifest = new DiagnosticBundleManifest(
            _versionService.DisplayVersion,
            DateTimeOffset.UtcNow,
            RuntimeInformation.OSDescription,
            Environment.Version.ToString(),
            preferencesSnapshot,
            connectionsSnapshot,
            alertsHistory,
            logSnapshot);

        ct.ThrowIfCancellationRequested();
        using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);
        WriteJsonEntry(archive, "manifest.json", manifest, ct);
        WriteJsonEntry(archive, "preferences.json", preferencesSnapshot, ct);
        WriteJsonEntry(archive, "alerts.json", new { rules = _alertService.Rules, history = alertsHistory }, ct);
        WriteJsonEntry(archive, "dashboard.json", new
        {
            current = _dashboardPersistenceService.Load(),
            presets = _dashboardPersistenceService.GetPresets()
        }, ct);
        WriteJsonEntry(archive, "logs.json", logSnapshot, ct);

        return bundlePath;
    }

    private IReadOnlyDictionary<string, object?> BuildPreferencesSnapshot()
    {
        return new Dictionary<string, object?>
        {
            ["Theme"] = _preferencesService.Theme,
            ["MessagesPerPage"] = _preferencesService.MessagesPerPage,
            ["MaxTotalMessages"] = _preferencesService.MaxTotalMessages,
            ["RestoreTabsOnStartup"] = _preferencesService.RestoreTabsOnStartup,
            ["OpenTabsJson"] = _preferencesService.OpenTabsJson
        };
    }

    private async Task<IReadOnlyList<object>> BuildConnectionsSnapshotAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var connections = await _connectionStorageService.GetConnectionsAsync();
        ct.ThrowIfCancellationRequested();

        return connections
            .Select(c => (object)new
            {
                c.Id,
                c.Name,
                c.Type,
                c.EntityName,
                c.CreatedAt,
                Endpoint = RedactSecrets(c.Endpoint),
                HasConnectionString = !string.IsNullOrWhiteSpace(c.ConnectionString)
            })
            .ToList();
    }

    private IReadOnlyList<LogEntry> BuildLogSnapshot()
    {
        return _logSink.GetLogs()
            .Select(log => log with
            {
                Message = RedactSecrets(log.Message) ?? string.Empty,
                Details = RedactSecrets(log.Details)
            })
            .ToList();
    }

    private static void WriteJsonEntry(ZipArchive archive, string entryName, object payload, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(Serialize(payload));
        ct.ThrowIfCancellationRequested();
    }

    private static string? RedactSecrets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redacted = SharedAccessKeyPattern.Replace(value, "SharedAccessKey=<redacted>");
        redacted = EndpointPattern.Replace(redacted, "Endpoint=sb://<redacted>/");
        return ServiceBusHostPattern.Replace(redacted, "<redacted>");
    }
}
