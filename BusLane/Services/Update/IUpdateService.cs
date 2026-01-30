namespace BusLane.Services.Update;

using BusLane.Models.Update;

public interface IUpdateService
{
    UpdateStatus Status { get; }
    ReleaseInfo? AvailableRelease { get; }
    double DownloadProgress { get; }
    string? ErrorMessage { get; }

    event EventHandler<UpdateStatus>? StatusChanged;
    event EventHandler<double>? DownloadProgressChanged;

    Task CheckForUpdatesAsync(bool manualCheck = false);
    Task DownloadUpdateAsync();
    Task InstallUpdateAsync();
    void SkipVersion(string version);
    void RemindLater(TimeSpan delay);
    void DismissNotification();
}
