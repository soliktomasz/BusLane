namespace BusLane.ViewModels;

using BusLane.Models.Update;
using BusLane.Services.Update;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class UpdateNotificationViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _versionText = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private string? _errorMessage;

    public UpdateNotificationViewModel(IUpdateService updateService)
    {
        _updateService = updateService;

        _updateService.StatusChanged += OnStatusChanged;
        _updateService.DownloadProgressChanged += OnDownloadProgressChanged;

        UpdateFromServiceState();
    }

    private void OnStatusChanged(object? sender, UpdateStatus status)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateFromServiceState();
        });
    }

    private void OnDownloadProgressChanged(object? sender, double progress)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DownloadProgress = progress;
        });
    }

    private void UpdateFromServiceState()
    {
        var status = _updateService.Status;
        var release = _updateService.AvailableRelease;

        IsVisible = status is UpdateStatus.UpdateAvailable
                              or UpdateStatus.Downloading
                              or UpdateStatus.Downloaded
                              or UpdateStatus.Error;

        IsDownloading = status == UpdateStatus.Downloading;
        IsDownloaded = status == UpdateStatus.Downloaded;
        ErrorMessage = _updateService.ErrorMessage;

        if (release != null)
        {
            VersionText = $"BusLane {release.Version} is available";
            ReleaseNotes = GetReleaseNotesSummary(release.ReleaseNotes);
        }
    }

    private static string GetReleaseNotesSummary(string notes)
    {
        if (string.IsNullOrEmpty(notes))
            return "Bug fixes and improvements.";

        var firstLine = notes.Split('\n')[0];
        if (firstLine.Length > 100)
            return firstLine[..100] + "...";
        return firstLine;
    }

    [RelayCommand]
    private async Task InstallNowAsync()
    {
        if (_updateService.Status == UpdateStatus.UpdateAvailable)
        {
            await _updateService.DownloadUpdateAsync();
        }
        else if (_updateService.Status == UpdateStatus.Downloaded)
        {
            await _updateService.InstallUpdateAsync();
        }
    }

    [RelayCommand]
    private void RemindLater()
    {
        _updateService.RemindLater(TimeSpan.FromDays(3));
        IsVisible = false;
    }

    [RelayCommand]
    private void SkipVersion()
    {
        var version = _updateService.AvailableRelease?.Version;
        if (version != null)
        {
            _updateService.SkipVersion(version);
        }
        IsVisible = false;
    }

    [RelayCommand]
    private void Dismiss()
    {
        _updateService.DismissNotification();
        IsVisible = false;
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        ErrorMessage = null;
        await _updateService.CheckForUpdatesAsync(manualCheck: true);
    }
}
