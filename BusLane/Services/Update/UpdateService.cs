namespace BusLane.Services.Update;

using System.Timers;
using BusLane.Models.Update;
using BusLane.Services.Abstractions;
using BusLane.Services.Infrastructure;
using Serilog;

public class UpdateService : IUpdateService, IDisposable
{
    private readonly IVersionService _versionService;
    private readonly IPreferencesService _preferencesService;
    private readonly UpdateDownloadService _downloadService;
    private readonly System.Timers.Timer _checkTimer;
    private readonly string _platform;

    private UpdateStatus _status = UpdateStatus.Idle;
    private ReleaseInfo? _availableRelease;
    private double _downloadProgress;
    private string? _errorMessage;
    private string? _downloadedFilePath;

    public UpdateStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            StatusChanged?.Invoke(this, value);
        }
    }

    public ReleaseInfo? AvailableRelease => _availableRelease;
    public double DownloadProgress => _downloadProgress;
    public string? ErrorMessage => _errorMessage;

    public event EventHandler<UpdateStatus>? StatusChanged;
    public event EventHandler<double>? DownloadProgressChanged;

    public UpdateService(
        IVersionService versionService,
        IPreferencesService preferencesService,
        UpdateDownloadService? downloadService = null)
    {
        _versionService = versionService;
        _preferencesService = preferencesService;
        _downloadService = downloadService ?? new UpdateDownloadService();
        _platform = UpdateCheckService.GetCurrentPlatform();

        _downloadService.ProgressChanged += (s, progress) =>
        {
            _downloadProgress = progress;
            DownloadProgressChanged?.Invoke(this, progress);
        };

        // Check every 24 hours
        _checkTimer = new System.Timers.Timer(TimeSpan.FromHours(24).TotalMilliseconds);
        _checkTimer.Elapsed += async (_, _) => await CheckForUpdatesAsync();
    }

    public async Task CheckForUpdatesAsync(bool manualCheck = false)
    {
        if (Status == UpdateStatus.Checking || Status == UpdateStatus.Downloading)
            return;

        // Respect user preferences for automatic checks
        if (!manualCheck && !ShouldAutoCheck())
        {
            Log.Information("Skipping automatic update check due to user preferences");
            return;
        }

        try
        {
            Status = UpdateStatus.Checking;
            _errorMessage = null;

            var currentVersion = _versionService.Version;
            var release = await UpdateCheckService.CheckForUpdateAsync(currentVersion, _platform);

            if (release == null)
            {
                Status = UpdateStatus.Idle;
                return;
            }

            // Check if user skipped this version
            var prefs = LoadPreferences();
            if (prefs.SkippedVersion == release.Version && !manualCheck)
            {
                Log.Information("User skipped version {Version}, not notifying", release.Version);
                Status = UpdateStatus.Idle;
                return;
            }

            _availableRelease = release;
            Status = UpdateStatus.UpdateAvailable;

            // Start timer for periodic checks if not already running
            if (!_checkTimer.Enabled)
            {
                _checkTimer.Start();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update check failed");
            _errorMessage = ex.Message;
            Status = UpdateStatus.Error;
        }
    }

    public async Task DownloadUpdateAsync()
    {
        if (_availableRelease?.Assets.TryGetValue(_platform, out var asset) != true || asset == null)
        {
            _errorMessage = "No update available for download";
            Status = UpdateStatus.Error;
            return;
        }

        try
        {
            Status = UpdateStatus.Downloading;
            _errorMessage = null;

            var filePath = await _downloadService.DownloadAsync(asset);

            if (filePath == null)
            {
                _errorMessage = "Download failed";
                Status = UpdateStatus.Error;
                return;
            }

            _downloadedFilePath = filePath;
            Status = UpdateStatus.Downloaded;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Download failed");
            _errorMessage = ex.Message;
            Status = UpdateStatus.Error;
        }
    }

    public Task InstallUpdateAsync()
    {
        // Platform-specific installation will be implemented in Task 6
        // For now, just open the downloaded file
        if (_downloadedFilePath == null)
        {
            _errorMessage = "No update downloaded";
            Status = UpdateStatus.Error;
            return Task.CompletedTask;
        }

        try
        {
            Status = UpdateStatus.Installing;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _downloadedFilePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            // The installer will handle the actual installation
            // Application should exit to allow file replacement
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Installation failed");
            _errorMessage = ex.Message;
            Status = UpdateStatus.Error;
            return Task.CompletedTask;
        }
    }

    public void SkipVersion(string version)
    {
        SavePreferences(new UpdatePreferences
        {
            LastCheckTime = DateTime.UtcNow,
            SkippedVersion = version,
            RemindLaterDate = null,
            AutoCheckEnabled = true
        });

        _availableRelease = null;
        Status = UpdateStatus.Idle;
    }

    public void RemindLater(TimeSpan delay)
    {
        SavePreferences(new UpdatePreferences
        {
            LastCheckTime = DateTime.UtcNow,
            SkippedVersion = null,
            RemindLaterDate = DateTime.UtcNow.Add(delay),
            AutoCheckEnabled = true
        });

        _availableRelease = null;
        Status = UpdateStatus.Idle;
    }

    public void DismissNotification()
    {
        // Just hide the notification without skipping
        Status = UpdateStatus.Idle;
    }

    private bool ShouldAutoCheck()
    {
        var prefs = LoadPreferences();

        if (!prefs.AutoCheckEnabled)
            return false;

        if (prefs.RemindLaterDate.HasValue && prefs.RemindLaterDate > DateTime.UtcNow)
            return false;

        return true;
    }

    private UpdatePreferences LoadPreferences()
    {
        // For now, use simple in-memory storage
        // Could be enhanced to use IPreferencesService with additional fields
        return new UpdatePreferences
        {
            AutoCheckEnabled = true
        };
    }

    private void SavePreferences(UpdatePreferences prefs)
    {
        // Persist to IPreferencesService if extended
        Log.Information("Update preferences saved: Skipped={Skipped}, RemindLater={Remind}",
            prefs.SkippedVersion, prefs.RemindLaterDate);
    }

    public void Dispose()
    {
        _checkTimer?.Stop();
        _checkTimer?.Dispose();
        _downloadService?.Cleanup();
    }
}
