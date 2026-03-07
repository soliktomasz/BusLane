namespace BusLane.Services.Diagnostics;

using System.IO.Compression;
using System.Runtime.InteropServices;
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
        Directory.CreateDirectory(_bundleDirectory);
        var bundlePath = Path.Combine(_bundleDirectory, $"buslane-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");

        var manifest = new DiagnosticBundleManifest(
            _versionService.DisplayVersion,
            DateTimeOffset.UtcNow,
            RuntimeInformation.OSDescription,
            Environment.Version.ToString(),
            BuildPreferencesSnapshot(),
            await BuildConnectionsSnapshotAsync(),
            _alertService.History,
            BuildLogSnapshot());

        using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);
        WriteJsonEntry(archive, "manifest.json", manifest);
        WriteJsonEntry(archive, "preferences.json", BuildPreferencesSnapshot());
        WriteJsonEntry(archive, "alerts.json", new { rules = _alertService.Rules, history = _alertService.History });
        WriteJsonEntry(archive, "dashboard.json", new
        {
            current = _dashboardPersistenceService.Load(),
            presets = _dashboardPersistenceService.GetPresets()
        });
        WriteJsonEntry(archive, "logs.json", BuildLogSnapshot());

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

    private async Task<IReadOnlyList<object>> BuildConnectionsSnapshotAsync()
    {
        var connections = await _connectionStorageService.GetConnectionsAsync();
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

    private static void WriteJsonEntry(ZipArchive archive, string entryName, object payload)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(Serialize(payload));
    }

    private static string? RedactSecrets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("SharedAccessKey=", "SharedAccessKey=<redacted>", StringComparison.OrdinalIgnoreCase)
            .Replace("Endpoint=sb://", "Endpoint=sb://<redacted>/", StringComparison.OrdinalIgnoreCase)
            .Replace(".servicebus.windows.net", ".<redacted>", StringComparison.OrdinalIgnoreCase);
    }
}
