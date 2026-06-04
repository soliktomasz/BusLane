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
    public async Task CheckForUpdatesNowCommand_WhenUpdateAvailable_ShowsInstallOption()
    {
        // Arrange
        var updateService = Substitute.For<IUpdateService>();
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
        var updateService = Substitute.For<IUpdateService>();
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
        var updateService = Substitute.For<IUpdateService>();
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

    private static SettingsViewModel CreateSut(IUpdateService updateService)
    {
        var preferences = Substitute.For<IPreferencesService>();
        preferences.Theme.Returns("System");
        preferences.DefaultMessageCount.Returns(100);
        preferences.MessagesPerPage.Returns(100);
        preferences.MaxTotalMessages.Returns(500);
        preferences.AutoRefreshIntervalSeconds.Returns(30);
        preferences.AutoCheckForUpdates.Returns(true);
        preferences.ShowDeadLetterBadges.Returns(true);
        preferences.EnableMessagePreview.Returns(true);
        preferences.RestoreTabsOnStartup.Returns(true);

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
}
