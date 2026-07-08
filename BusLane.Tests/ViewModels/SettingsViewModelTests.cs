namespace BusLane.Tests.ViewModels;

using BusLane.Models.Update;
using BusLane.Services.Abstractions;
using BusLane.Services.Security;
using BusLane.Services.Update;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_LoadsTopicActionButtonPreference()
    {
        // Arrange
        var preferences = CreatePreferences();
        preferences.ShowTopicActionButtons.Returns(false);
        var updateService = CreateUpdateService();

        // Act
        var sut = CreateSut(updateService, preferences);

        // Assert
        sut.ShowTopicActionButtons.Should().BeFalse();
    }

    [Fact]
    public void SaveSettingsCommand_SavesTopicActionButtonPreference()
    {
        // Arrange
        var preferences = CreatePreferences();
        var updateService = CreateUpdateService();
        var sut = CreateSut(updateService, preferences);

        // Act
        sut.ShowTopicActionButtons = false;
        sut.SaveSettingsCommand.Execute(null);

        // Assert
        preferences.ShowTopicActionButtons.Should().BeFalse();
        preferences.Received(1).Save();
    }

    [Fact]
    public void ResetToDefaultsCommand_RestoresTopicActionButtonVisibility()
    {
        // Arrange
        var preferences = CreatePreferences();
        preferences.ShowTopicActionButtons.Returns(false);
        var updateService = CreateUpdateService();
        var sut = CreateSut(updateService, preferences);
        sut.ShowTopicActionButtons = false;

        // Act
        sut.ResetToDefaultsCommand.Execute(null);

        // Assert
        sut.ShowTopicActionButtons.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdatesNowCommand_WhenUpdateAvailable_ShowsInstallOption()
    {
        // Arrange
        var updateService = CreateUpdateService();
        updateService.CanSelfUpdate.Returns(true);
        updateService.Status.Returns(UpdateStatus.Idle);
        updateService.StatusMessage.Returns("Ready to check for updates.");
        updateService.CheckForUpdatesAsync(true).Returns(_ =>
        {
            updateService.Status.Returns(UpdateStatus.UpdateAvailable);
            updateService.AvailableRelease.Returns(new ReleaseInfo { Version = "1.2.3" });
            updateService.StatusMessage.Returns("BusLane 1.2.3 is available.");
            return Task.CompletedTask;
        });
        var sut = CreateSut(updateService);

        // Act
        await sut.CheckForUpdatesNowCommand.ExecuteAsync(null);

        // Assert
        sut.ShowUpdateInstallAction.Should().BeTrue();
        sut.UpdateInstallActionText.Should().Be("Download update");
        sut.CanInstallUpdate.Should().BeTrue();
    }

    [Fact]
    public async Task InstallUpdateCommand_WhenUpdateAvailable_DownloadsUpdate()
    {
        // Arrange
        var updateService = CreateUpdateService();
        updateService.CanSelfUpdate.Returns(true);
        updateService.Status.Returns(UpdateStatus.UpdateAvailable);
        updateService.AvailableRelease.Returns(new ReleaseInfo { Version = "1.2.3" });
        updateService.StatusMessage.Returns("BusLane 1.2.3 is available.");
        var sut = CreateSut(updateService);

        // Act
        await sut.InstallUpdateCommand.ExecuteAsync(null);

        // Assert
        await updateService.Received(1).DownloadUpdateAsync();
        await updateService.DidNotReceive().InstallUpdateAsync();
    }

    [Fact]
    public async Task InstallUpdateCommand_WhenReadyToRestart_InstallsUpdate()
    {
        // Arrange
        var updateService = CreateUpdateService();
        updateService.CanSelfUpdate.Returns(true);
        updateService.Status.Returns(UpdateStatus.ReadyToRestart);
        updateService.AvailableRelease.Returns(new ReleaseInfo { Version = "1.2.3", IsReadyToRestart = true });
        updateService.StatusMessage.Returns("BusLane 1.2.3 is ready to install.");
        var sut = CreateSut(updateService);

        // Act
        await sut.InstallUpdateCommand.ExecuteAsync(null);

        // Assert
        await updateService.Received(1).InstallUpdateAsync();
        await updateService.DidNotReceive().DownloadUpdateAsync();
    }

    [Fact]
    public async Task InstallUpdateCommand_WhenDownloadFails_ShowsErrorAndResetsInstallingState()
    {
        // Arrange
        var updateService = CreateUpdateService();
        updateService.CanSelfUpdate.Returns(true);
        updateService.Status.Returns(UpdateStatus.UpdateAvailable);
        updateService.AvailableRelease.Returns(new ReleaseInfo { Version = "1.2.3" });
        updateService.StatusMessage.Returns("BusLane 1.2.3 is available.");
        updateService.DownloadUpdateAsync().Returns<Task>(_ => throw new InvalidOperationException("download failed"));
        var sut = CreateSut(updateService);

        // Act
        var act = async () => await sut.InstallUpdateCommand.ExecuteAsync(null);

        // Assert
        await act.Should().NotThrowAsync();
        sut.IsInstallingUpdate.Should().BeFalse();
        sut.UpdateStatusMessage.Should().Be("Update install failed: download failed");
    }

    [Fact]
    public void Dispose_WhenStatusChangedRaisedAfterDispose_DoesNotRefreshCachedStatus()
    {
        // Arrange
        var updateService = CreateUpdateService();
        updateService.CanSelfUpdate.Returns(true);
        updateService.Status.Returns(UpdateStatus.Idle);
        updateService.StatusMessage.Returns("Ready to check for updates.");
        var sut = CreateSut(updateService);

        // Act
        sut.Dispose();
        updateService.Status.Returns(UpdateStatus.UpdateAvailable);
        updateService.AvailableRelease.Returns(new ReleaseInfo { Version = "1.2.3" });
        updateService.StatusMessage.Returns("BusLane 1.2.3 is available.");
        updateService.StatusChanged += Raise.Event<EventHandler<UpdateStatus>>(updateService, UpdateStatus.UpdateAvailable);

        // Assert
        sut.UpdateStatusMessage.Should().Be("Ready to check for updates.");
        updateService.DidNotReceive().DownloadUpdateAsync();
        updateService.DidNotReceive().InstallUpdateAsync();
    }

    private static SettingsViewModel CreateSut(IUpdateService updateService, IPreferencesService? preferences = null)
    {
        preferences ??= CreatePreferences();

        var appLockService = Substitute.For<IAppLockService>();
        var biometricAuthService = Substitute.For<IBiometricAuthService>();

        return new SettingsViewModel(
            () => { },
            preferences,
            appLockService,
            biometricAuthService,
            _ => Task.CompletedTask,
            updateService: updateService);
    }

    private static IPreferencesService CreatePreferences()
    {
        var preferences = Substitute.For<IPreferencesService>();
        preferences.Theme.Returns("System");
        preferences.DefaultMessageCount.Returns(100);
        preferences.MessagesPerPage.Returns(100);
        preferences.AutoRefreshIntervalSeconds.Returns(30);
        preferences.AutoCheckForUpdates.Returns(true);
        preferences.ShowDeadLetterBadges.Returns(true);
        preferences.EnableMessagePreview.Returns(true);
        preferences.RestoreTabsOnStartup.Returns(true);
        preferences.ShowTopicActionButtons.Returns(true);
        return preferences;
    }

    private static IUpdateService CreateUpdateService()
    {
        return Substitute.For<IUpdateService>();
    }
}
