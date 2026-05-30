namespace BusLane.Tests.ViewModels;

using BusLane.Models.Update;
using BusLane.Services.Update;
using BusLane.ViewModels;
using FluentAssertions;

public class UpdateNotificationViewModelTests
{
    [Fact]
    public void Constructor_WhenUpdateAvailable_ShowsDownloadAction()
    {
        // Arrange
        var service = new FakeUpdateService
        {
            Status = UpdateStatus.UpdateAvailable,
            AvailableRelease = new ReleaseInfo { Version = "1.0.0", ReleaseNotes = "Notes" },
            StatusMessage = "BusLane 1.0.0 is available."
        };

        // Act
        var sut = new UpdateNotificationViewModel(service);

        // Assert
        sut.IsVisible.Should().BeTrue();
        sut.PrimaryActionText.Should().Be("Download");
        sut.VersionText.Should().Be("BusLane 1.0.0 is available");
    }

    [Fact]
    public void Constructor_WhenReadyToRestart_ShowsRestartAction()
    {
        // Arrange
        var service = new FakeUpdateService
        {
            Status = UpdateStatus.ReadyToRestart,
            AvailableRelease = new ReleaseInfo { Version = "1.0.0", IsReadyToRestart = true },
            StatusMessage = "BusLane 1.0.0 is ready to install."
        };

        // Act
        var sut = new UpdateNotificationViewModel(service);

        // Assert
        sut.IsVisible.Should().BeTrue();
        sut.PrimaryActionText.Should().Be("Restart");
        sut.IsReadyToRestart.Should().BeTrue();
    }

    [Fact]
    public async Task InstallNowCommand_WhenUpdateAvailable_DownloadsUpdate()
    {
        // Arrange
        var service = new FakeUpdateService
        {
            Status = UpdateStatus.UpdateAvailable,
            AvailableRelease = new ReleaseInfo { Version = "1.0.0" }
        };
        var sut = new UpdateNotificationViewModel(service);

        // Act
        await sut.InstallNowCommand.ExecuteAsync(null);

        // Assert
        service.DownloadUpdateCallCount.Should().Be(1);
        service.InstallUpdateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task InstallNowCommand_WhenReadyToRestart_InstallsUpdate()
    {
        // Arrange
        var service = new FakeUpdateService
        {
            Status = UpdateStatus.ReadyToRestart,
            AvailableRelease = new ReleaseInfo { Version = "1.0.0", IsReadyToRestart = true }
        };
        var sut = new UpdateNotificationViewModel(service);

        // Act
        await sut.InstallNowCommand.ExecuteAsync(null);

        // Assert
        service.DownloadUpdateCallCount.Should().Be(0);
        service.InstallUpdateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InstallNowCommand_WhenDownloading_DoesNothing()
    {
        // Arrange
        var service = new FakeUpdateService
        {
            Status = UpdateStatus.Downloading,
            AvailableRelease = new ReleaseInfo { Version = "1.0.0" }
        };
        var sut = new UpdateNotificationViewModel(service);

        // Act
        await sut.InstallNowCommand.ExecuteAsync(null);

        // Assert
        service.DownloadUpdateCallCount.Should().Be(0);
        service.InstallUpdateCallCount.Should().Be(0);
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public UpdateStatus Status { get; set; }
        public ReleaseInfo? AvailableRelease { get; set; }
        public double DownloadProgress { get; set; }
        public string? ErrorMessage { get; set; }
        public bool CanSelfUpdate { get; set; } = true;
        public string StatusMessage { get; set; } = string.Empty;
        public int DownloadUpdateCallCount { get; private set; }
        public int InstallUpdateCallCount { get; private set; }
        public event EventHandler<UpdateStatus>? StatusChanged;
        public event EventHandler<double>? DownloadProgressChanged;
        public Task CheckForUpdatesAsync(bool manualCheck = false) => Task.CompletedTask;
        public Task DownloadUpdateAsync()
        {
            DownloadUpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task InstallUpdateAsync()
        {
            InstallUpdateCallCount++;
            return Task.CompletedTask;
        }

        public void SkipVersion(string version) { }
        public void RemindLater(TimeSpan delay) { }
        public void DismissNotification() { }
        public void RaiseStatusChanged(UpdateStatus status) => StatusChanged?.Invoke(this, status);
        public void RaiseDownloadProgressChanged(double progress) => DownloadProgressChanged?.Invoke(this, progress);
    }
}
