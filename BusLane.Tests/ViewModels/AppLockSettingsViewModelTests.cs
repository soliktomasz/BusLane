namespace BusLane.Tests.ViewModels;

using BusLane.Models.Security;
using BusLane.Services.Security;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public sealed class AppLockSettingsViewModelTests
{
    [Fact]
    public async Task InitializeAsync_WithEnabledLockAndAvailableBiometrics_LoadsSecurityState()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: true));

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        biometricAuthService.GetAvailabilityAsync(Arg.Any<CancellationToken>())
            .Returns(BiometricAvailability.Available);

        var sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.IsEnabled.Should().BeTrue();
        sut.CanConfigureBiometrics.Should().BeTrue();
        sut.BiometricUnlockEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task EnableAppLockAsync_WithValidPasswordAndAcknowledgement_PersistsAndTrustsCurrentSession()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: false, BiometricUnlockEnabled: false));
        appLockService.EnableAsync(Arg.Any<AppLockConfiguration>(), Arg.Any<CancellationToken>())
            .Returns("ABCD-EFGH-IJKL-MNOP");

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        biometricAuthService.GetAvailabilityAsync(Arg.Any<CancellationToken>())
            .Returns(BiometricAvailability.Available);

        AppLockSnapshot? appliedSnapshot = null;
        var sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, snapshot =>
        {
            appliedSnapshot = snapshot;
            return Task.CompletedTask;
        });

        await sut.InitializeAsync();
        sut.NewPassword = "Enable#1";
        sut.ConfirmPassword = "Enable#1";
        sut.EnableBiometricUnlock = true;
        sut.HasStoredRecoveryCode = true;

        // Act
        await sut.EnableAppLockCommand.ExecuteAsync(null);

        // Assert
        await appLockService.Received(1).EnableAsync(
            Arg.Is<AppLockConfiguration>(config => config.Password == "Enable#1" && config.BiometricUnlockEnabled),
            Arg.Any<CancellationToken>());
        sut.RecoveryCode.Should().Be("ABCD-EFGH-IJKL-MNOP");
        sut.IsEnabled.Should().BeTrue();
        appliedSnapshot.Should().Be(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: true));
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCurrentPassword_ReauthenticatesBeforeSaving()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));
        appLockService.VerifyPasswordAsync("Current#1", Arg.Any<CancellationToken>())
            .Returns(true);

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);

        await sut.InitializeAsync();
        sut.CurrentPassword = "Current#1";
        sut.ChangePassword = "Changed#2";
        sut.ConfirmChangePassword = "Changed#2";

        // Act
        await sut.ChangePasswordCommand.ExecuteAsync(null);

        // Assert
        await appLockService.Received(1).VerifyPasswordAsync("Current#1", Arg.Any<CancellationToken>());
        await appLockService.Received(1).ChangePasswordAsync("Changed#2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableAppLockAsync_WithBiometricReauthentication_DisablesLock()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: true));

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        biometricAuthService.GetAvailabilityAsync(Arg.Any<CancellationToken>())
            .Returns(BiometricAvailability.Available);
        biometricAuthService.AuthenticateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(BiometricAuthResult.Success);

        AppLockSnapshot? appliedSnapshot = null;
        var sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, snapshot =>
        {
            appliedSnapshot = snapshot;
            return Task.CompletedTask;
        });

        await sut.InitializeAsync();

        // Act
        await sut.DisableAppLockCommand.ExecuteAsync(null);

        // Assert
        await appLockService.Received(1).DisableAsync(Arg.Any<CancellationToken>());
        sut.IsEnabled.Should().BeFalse();
        appliedSnapshot.Should().Be(AppLockSnapshot.Disabled);
    }

    [Fact]
    public async Task ToggleBiometricUnlockAsync_WhenUnsupported_ShowsErrorWithoutSaving()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        biometricAuthService.GetAvailabilityAsync(Arg.Any<CancellationToken>())
            .Returns(BiometricAvailability.Unavailable);

        var sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);
        await sut.InitializeAsync();

        // Act
        await sut.ToggleBiometricUnlockCommand.ExecuteAsync(true);

        // Assert
        sut.ErrorMessage.Should().Be("Biometric unlock is unavailable on this device.");
        await appLockService.DidNotReceive().UpdateBiometricPreferenceAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
