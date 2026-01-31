namespace BusLane.Services.Update;

using BusLane.Models.Update;

/// <summary>
/// Manages application update lifecycle: check, download, and install.
/// </summary>
public interface IUpdateService
{
    /// <summary>Gets the current update status.</summary>
    UpdateStatus Status { get; }

    /// <summary>Gets the available release info, if an update was found.</summary>
    ReleaseInfo? AvailableRelease { get; }

    /// <summary>Gets the current download progress (0-100).</summary>
    double DownloadProgress { get; }

    /// <summary>Gets the error message from the last failed operation.</summary>
    string? ErrorMessage { get; }

    /// <summary>Raised when the update status changes.</summary>
    event EventHandler<UpdateStatus>? StatusChanged;

    /// <summary>Raised when download progress changes.</summary>
    event EventHandler<double>? DownloadProgressChanged;

    /// <summary>Checks for available updates from the remote repository.</summary>
    /// <param name="manualCheck">If true, ignores user preferences like remind-later and skipped versions.</param>
    Task CheckForUpdatesAsync(bool manualCheck = false);

    /// <summary>Downloads the available update to a temporary location.</summary>
    Task DownloadUpdateAsync();

    /// <summary>Opens the release page in the browser for manual installation.</summary>
    Task InstallUpdateAsync();

    /// <summary>Marks the specified version as skipped so it won't trigger future notifications.</summary>
    void SkipVersion(string version);

    /// <summary>Defers the update notification for the specified duration.</summary>
    void RemindLater(TimeSpan delay);

    /// <summary>Dismisses the current notification without skipping the version.</summary>
    void DismissNotification();
}
