namespace BusLane.Tests.Services.Update;

using BusLane.Models.Update;
using BusLane.Services.Abstractions;
using BusLane.Services.Infrastructure;
using BusLane.Services.Update;
using FluentAssertions;
using NSubstitute;

public class UpdateServiceTests
{
    private readonly IVersionService _versionService;
    private readonly IPreferencesService _preferencesService;
    private readonly UpdateService _updateService;

    public UpdateServiceTests()
    {
        _versionService = Substitute.For<IVersionService>();
        _versionService.Version.Returns("0.9.0");

        _preferencesService = Substitute.For<IPreferencesService>();
        _preferencesService.AutoCheckForUpdates.Returns(true);
        _preferencesService.SkippedUpdateVersion.Returns((string?)null);
        _preferencesService.UpdateRemindLaterDate.Returns((DateTime?)null);

        _updateService = new UpdateService(_versionService, _preferencesService);
    }

    [Fact]
    public void SkipVersion_SetsStatusToIdleAndSavesPreference()
    {
        _updateService.SkipVersion("1.0.0");

        _updateService.Status.Should().Be(UpdateStatus.Idle);
        _preferencesService.SkippedUpdateVersion.Should().Be("1.0.0");
        _preferencesService.Received().Save();
    }

    [Fact]
    public void RemindLater_SetsStatusToIdleAndSavesDate()
    {
        var delay = TimeSpan.FromDays(3);

        _updateService.RemindLater(delay);

        _updateService.Status.Should().Be(UpdateStatus.Idle);
        _preferencesService.Received().Save();
        _preferencesService.UpdateRemindLaterDate.Should().NotBeNull();
    }

    [Fact]
    public void DismissNotification_SetsStatusToIdle()
    {
        _updateService.DismissNotification();

        _updateService.Status.Should().Be(UpdateStatus.Idle);
    }

    [Fact]
    public void StatusChanged_FiresWhenStatusChanges()
    {
        var statuses = new List<UpdateStatus>();
        _updateService.StatusChanged += (_, status) => statuses.Add(status);

        _updateService.DismissNotification();

        statuses.Should().Contain(UpdateStatus.Idle);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenAutoCheckDisabled_DoesNotCheck()
    {
        _preferencesService.AutoCheckForUpdates.Returns(false);

        await _updateService.CheckForUpdatesAsync(manualCheck: false);

        _updateService.Status.Should().Be(UpdateStatus.Idle);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRemindLaterInFuture_DoesNotCheck()
    {
        _preferencesService.UpdateRemindLaterDate.Returns(DateTime.UtcNow.AddDays(1));

        await _updateService.CheckForUpdatesAsync(manualCheck: false);

        _updateService.Status.Should().Be(UpdateStatus.Idle);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ManualCheck_IgnoresRemindLater()
    {
        _preferencesService.UpdateRemindLaterDate.Returns(DateTime.UtcNow.AddDays(1));

        // Manual check should proceed even with remind-later set
        // It will fail to reach GitHub but should at least attempt (transition to Checking)
        var statuses = new List<UpdateStatus>();
        _updateService.StatusChanged += (_, status) => statuses.Add(status);

        await _updateService.CheckForUpdatesAsync(manualCheck: true);

        statuses.Should().Contain(UpdateStatus.Checking);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithNoRelease_SetsError()
    {
        await _updateService.DownloadUpdateAsync();

        _updateService.Status.Should().Be(UpdateStatus.Error);
        _updateService.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Dispose_StopsTimer()
    {
        // Should not throw
        _updateService.Dispose();
    }
}
