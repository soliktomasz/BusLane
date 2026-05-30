namespace BusLane.Tests.Services.Update;

using BusLane.Models.Update;
using BusLane.Services.Abstractions;
using BusLane.Services.Infrastructure;
using BusLane.Services.Update;
using FluentAssertions;
using NSubstitute;

public class UpdateServiceTests
{
    private readonly IVersionService _versionService = Substitute.For<IVersionService>();
    private readonly IPreferencesService _preferencesService = Substitute.For<IPreferencesService>();
    private readonly FakeVelopackUpdateManager _manager = new();

    public UpdateServiceTests()
    {
        _versionService.Version.Returns("0.9.0");
        _versionService.InformationalVersion.Returns("0.9.0");
        _preferencesService.AutoCheckForUpdates.Returns(true);
        _preferencesService.SkippedUpdateVersion.Returns((string?)null);
        _preferencesService.UpdateRemindLaterDate.Returns((DateTime?)null);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNotVelopackInstalled_ShowsNotInstalledForManualCheck()
    {
        // Arrange
        var sut = CreateSut();
        _manager.IsInstalled = false;

        // Act
        await sut.CheckForUpdatesAsync(manualCheck: true);

        // Assert
        sut.Status.Should().Be(UpdateStatus.NotInstalled);
        sut.CanSelfUpdate.Should().BeFalse();
        sut.StatusMessage.Should().Contain("GitHub Releases");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNoUpdateFound_ShowsUpToDateForManualCheck()
    {
        // Arrange
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = null;

        // Act
        await sut.CheckForUpdatesAsync(manualCheck: true);

        // Assert
        sut.Status.Should().Be(UpdateStatus.UpToDate);
        sut.AvailableRelease.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdateFound_ShowsUpdateAvailable()
    {
        // Arrange
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);

        // Act
        await sut.CheckForUpdatesAsync(manualCheck: true);

        // Assert
        sut.Status.Should().Be(UpdateStatus.UpdateAvailable);
        sut.AvailableRelease.Should().NotBeNull();
        sut.AvailableRelease!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenUpdateAvailable_ReportsProgressAndReadiesRestart()
    {
        // Arrange
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);
        var progress = new List<double>();
        sut.DownloadProgressChanged += (_, value) => progress.Add(value);

        // Act
        await sut.CheckForUpdatesAsync(manualCheck: true);
        await sut.DownloadUpdateAsync();

        // Assert
        sut.Status.Should().Be(UpdateStatus.ReadyToRestart);
        sut.AvailableRelease!.IsReadyToRestart.Should().BeTrue();
        progress.Should().Contain(42);
        progress.Should().Contain(100);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenReadyToRestart_DelegatesToVelopack()
    {
        // Arrange
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);

        // Act
        await sut.CheckForUpdatesAsync(manualCheck: true);
        await sut.DownloadUpdateAsync();
        await sut.InstallUpdateAsync();

        // Assert
        _manager.AppliedUpdate.Should().NotBeNull();
        _manager.AppliedUpdate!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenSkippedVersionAndAutomaticCheck_DoesNotNotify()
    {
        // Arrange
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);
        _preferencesService.SkippedUpdateVersion.Returns("1.0.0");

        // Act
        await sut.CheckForUpdatesAsync(manualCheck: false);

        // Assert
        sut.Status.Should().Be(UpdateStatus.Idle);
        sut.AvailableRelease.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ManualCheck_IgnoresRemindLater()
    {
        // Arrange
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);
        _preferencesService.UpdateRemindLaterDate.Returns(DateTime.UtcNow.AddDays(1));

        // Act
        await sut.CheckForUpdatesAsync(manualCheck: true);

        // Assert
        sut.Status.Should().Be(UpdateStatus.UpdateAvailable);
    }

    [Fact]
    public void SkipVersion_SetsStatusToIdleAndSavesPreference()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.SkipVersion("1.0.0");

        // Assert
        sut.Status.Should().Be(UpdateStatus.Idle);
        _preferencesService.SkippedUpdateVersion.Should().Be("1.0.0");
        _preferencesService.Received().Save();
    }

    [Fact]
    public void RemindLater_SetsStatusToIdleAndSavesDate()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.RemindLater(TimeSpan.FromDays(3));

        // Assert
        sut.Status.Should().Be(UpdateStatus.Idle);
        _preferencesService.UpdateRemindLaterDate.Should().NotBeNull();
        _preferencesService.Received().Save();
    }

    private UpdateService CreateSut() => new(_versionService, _preferencesService, _manager);

    private sealed class FakeVelopackUpdateManager : IVelopackUpdateManager
    {
        public bool IsInstalled { get; set; } = true;
        public VelopackUpdateInfo? PendingUpdate { get; set; }
        public VelopackUpdateInfo? UpdateToReturn { get; set; }
        public VelopackUpdateInfo? AppliedUpdate { get; private set; }

        public Task<VelopackUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(UpdateToReturn);
        }

        public Task DownloadUpdatesAsync(VelopackUpdateInfo update, Action<int> progress, CancellationToken ct = default)
        {
            progress(42);
            progress(100);
            PendingUpdate = update;
            return Task.CompletedTask;
        }

        public void ApplyUpdatesAndRestart(VelopackUpdateInfo update)
        {
            AppliedUpdate = update;
        }
    }
}
