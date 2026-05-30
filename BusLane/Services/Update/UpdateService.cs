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
    private readonly IVelopackUpdateManager _updateManager;
    private readonly System.Timers.Timer _checkTimer;

    private UpdateStatus _status = UpdateStatus.Idle;
    private ReleaseInfo? _availableRelease;
    private VelopackUpdateInfo? _availableUpdate;
    private double _downloadProgress;
    private string? _errorMessage;
    private string _statusMessage = string.Empty;
    private bool _canSelfUpdate;

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
    public bool CanSelfUpdate
    {
        get => _canSelfUpdate;
        private set => _canSelfUpdate = value;
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => _statusMessage = value;
    }

    public event EventHandler<UpdateStatus>? StatusChanged;
    public event EventHandler<double>? DownloadProgressChanged;

    public UpdateService(
        IVersionService versionService,
        IPreferencesService preferencesService,
        IVelopackUpdateManager updateManager)
    {
        _versionService = versionService;
        _preferencesService = preferencesService;
        _updateManager = updateManager;
        CanSelfUpdate = _updateManager.IsInstalled;
        StatusMessage = CanSelfUpdate
            ? "Updates are managed by Velopack."
            : "Self-update is unavailable for this build. Download updates from GitHub Releases.";

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
        {
            return;
        }

        if (!manualCheck && !ShouldAutoCheck())
        {
            Log.Information("Skipping automatic update check due to user preferences");
            return;
        }

        try
        {
            _errorMessage = null;

            if (!_updateManager.IsInstalled)
            {
                CanSelfUpdate = false;
                StatusMessage = "Self-update is unavailable for this build. Download updates from GitHub Releases.";
                Status = manualCheck ? UpdateStatus.NotInstalled : UpdateStatus.Idle;
                return;
            }

            CanSelfUpdate = true;

            var pending = _updateManager.PendingUpdate;
            if (pending != null)
            {
                _availableUpdate = pending;
                _availableRelease = ToReleaseInfo(pending, readyToRestart: true);
                StatusMessage = $"BusLane {pending.Version} is ready to install.";
                Status = UpdateStatus.ReadyToRestart;
                return;
            }

            Status = UpdateStatus.Checking;
            var update = await _updateManager.CheckForUpdatesAsync();
            if (update == null)
            {
                _availableUpdate = null;
                _availableRelease = null;
                StatusMessage = manualCheck ? "BusLane is up to date." : string.Empty;
                Status = manualCheck ? UpdateStatus.UpToDate : UpdateStatus.Idle;
                return;
            }

            var prefs = LoadPreferences();
            if (!manualCheck && prefs.SkippedVersion == update.Version)
            {
                Log.Information("User skipped version {Version}, not notifying", update.Version);
                _availableUpdate = null;
                _availableRelease = null;
                Status = UpdateStatus.Idle;
                return;
            }

            _availableUpdate = update;
            _availableRelease = ToReleaseInfo(update, readyToRestart: false);
            StatusMessage = $"BusLane {update.Version} is available.";
            Status = UpdateStatus.UpdateAvailable;
        }
        catch (Exception ex)
        {
            if (manualCheck)
            {
                Log.Error(ex, "Manual update check failed");
                _errorMessage = ex.Message;
                StatusMessage = $"Update check failed: {ex.Message}";
                Status = UpdateStatus.Error;
            }
            else
            {
                Log.Warning(ex, "Automatic update check failed");
                _errorMessage = null;
                StatusMessage = string.Empty;
                Status = UpdateStatus.Idle;
            }
        }
    }

    public async Task DownloadUpdateAsync()
    {
        if (_availableUpdate == null || _availableRelease == null)
        {
            _errorMessage = "No update available for download";
            StatusMessage = _errorMessage;
            Status = UpdateStatus.Error;
            return;
        }

        try
        {
            Status = UpdateStatus.Downloading;
            _errorMessage = null;
            StatusMessage = $"Downloading BusLane {_availableUpdate.Version}...";

            await _updateManager.DownloadUpdatesAsync(_availableUpdate, progress =>
            {
                _downloadProgress = progress;
                DownloadProgressChanged?.Invoke(this, progress);
            });

            _availableRelease = _availableRelease with { IsReadyToRestart = true };
            StatusMessage = $"BusLane {_availableUpdate.Version} is ready to install.";
            Status = UpdateStatus.ReadyToRestart;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Download failed");
            _errorMessage = ex.Message;
            StatusMessage = $"Update download failed: {ex.Message}";
            Status = UpdateStatus.Error;
        }
    }

    public Task InstallUpdateAsync()
    {
        if (_availableUpdate == null || _availableRelease == null)
        {
            _errorMessage = "No update available";
            StatusMessage = _errorMessage;
            Status = UpdateStatus.Error;
            return Task.CompletedTask;
        }

        try
        {
            Status = UpdateStatus.Installing;
            StatusMessage = $"Restarting to install BusLane {_availableUpdate.Version}...";
            _updateManager.ApplyUpdatesAndRestart(_availableUpdate);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply update");
            _errorMessage = ex.Message;
            StatusMessage = $"Update install failed: {ex.Message}";
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

        _availableUpdate = null;
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

        _availableUpdate = null;
        _availableRelease = null;
        Status = UpdateStatus.Idle;
    }

    public void DismissNotification()
    {
        Status = UpdateStatus.Idle;
    }

    private bool ShouldAutoCheck()
    {
        var prefs = LoadPreferences();

        if (!prefs.AutoCheckEnabled)
        {
            return false;
        }

        if (prefs.RemindLaterDate.HasValue && prefs.RemindLaterDate > DateTime.UtcNow)
        {
            return false;
        }

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

    private static ReleaseInfo ToReleaseInfo(VelopackUpdateInfo update, bool readyToRestart) => new()
    {
        Version = update.Version,
        ReleaseNotes = update.ReleaseNotes,
        PublishedAt = update.PublishedAt,
        CanSelfUpdate = true,
        IsReadyToRestart = readyToRestart
    };

    public void Dispose()
    {
        _checkTimer.Stop();
        _checkTimer.Dispose();
    }
}
