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
        _checkTimer.Elapsed += OnCheckTimerElapsed;
        _checkTimer.Start();
    }

    private async void OnCheckTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            await CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception in periodic update check");
        }
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

            var currentVersion = _versionService.InformationalVersion;
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

        }
        catch (Exception ex)
        {
            if (manualCheck)
            {
                Log.Error(ex, "Manual update check failed");
                _errorMessage = ex.Message;
                Status = UpdateStatus.Error;
            }
            else
            {
                Log.Warning(ex, "Automatic update check failed");
                _errorMessage = null;
                Status = UpdateStatus.Idle;
            }
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
        if (_availableRelease == null)
        {
            _errorMessage = "No update available";
            Status = UpdateStatus.Error;
            return Task.CompletedTask;
        }

        try
        {
            // Open the GitHub release page in the user's browser so they can
            // download and verify the installer themselves. Direct execution of
            // downloaded binaries is disabled until checksum validation is implemented.
            var releaseUrl = _availableRelease.ReleaseUrl;
            Log.Information("Opening release page for manual install: {Url}", releaseUrl);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = releaseUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            Status = UpdateStatus.Idle;
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open release page");
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
        return new UpdatePreferences
        {
            AutoCheckEnabled = _preferencesService.AutoCheckForUpdates,
            SkippedVersion = _preferencesService.SkippedUpdateVersion,
            RemindLaterDate = _preferencesService.UpdateRemindLaterDate,
            LastCheckTime = null
        };
    }

    private void SavePreferences(UpdatePreferences prefs)
    {
        _preferencesService.AutoCheckForUpdates = prefs.AutoCheckEnabled;
        _preferencesService.SkippedUpdateVersion = prefs.SkippedVersion;
        _preferencesService.UpdateRemindLaterDate = prefs.RemindLaterDate;
        _preferencesService.Save();

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
