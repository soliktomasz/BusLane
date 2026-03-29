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

        var _sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);

        // Act
        await _sut.InitializeAsync();

        // Assert
        _sut.IsEnabled.Should().BeTrue();
        _sut.CanConfigureBiometrics.Should().BeTrue();
        _sut.BiometricUnlockEnabled.Should().BeTrue();
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
        var _sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, snapshot =>
        {
            appliedSnapshot = snapshot;
            return Task.CompletedTask;
        });

        await _sut.InitializeAsync();
        _sut.NewPassword = "Enable#1";
        _sut.ConfirmPassword = "Enable#1";
        _sut.EnableBiometricUnlock = true;
        _sut.HasStoredRecoveryCode = true;

        // Act
        await _sut.EnableAppLockCommand.ExecuteAsync(null);

        // Assert
        await appLockService.Received(1).EnableAsync(
            Arg.Is<AppLockConfiguration>(config => config.Password == "Enable#1" && config.BiometricUnlockEnabled),
            Arg.Any<CancellationToken>());
        _sut.RecoveryCode.Should().Be("ABCD-EFGH-IJKL-MNOP");
        _sut.IsEnabled.Should().BeTrue();
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
        var _sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);

        await _sut.InitializeAsync();
        _sut.CurrentPassword = "Current#1";
        _sut.ChangePassword = "Changed#2";
        _sut.ConfirmChangePassword = "Changed#2";

        // Act
        await _sut.ChangePasswordCommand.ExecuteAsync(null);

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
        var _sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, snapshot =>
        {
            appliedSnapshot = snapshot;
            return Task.CompletedTask;
        });

        await _sut.InitializeAsync();

        // Act
        await _sut.DisableAppLockCommand.ExecuteAsync(null);

        // Assert
        await appLockService.Received(1).DisableAsync(Arg.Any<CancellationToken>());
        _sut.IsEnabled.Should().BeFalse();
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

        var _sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);
        await _sut.InitializeAsync();

        // Act
        await _sut.ToggleBiometricUnlockCommand.ExecuteAsync(true);

        // Assert
        _sut.ErrorMessage.Should().Be("Biometric unlock is unavailable on this device.");
        await appLockService.DidNotReceive().UpdateBiometricPreferenceAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_WhenLoadingFails_SetsSafeDefaultsAndErrorMessage()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns<Task<AppLockSnapshot>>(_ => Task.FromException<AppLockSnapshot>(new InvalidOperationException("Load failed")));

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var _sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);

        // Act
        await _sut.InitializeAsync();

        // Assert
        _sut.IsEnabled.Should().BeFalse();
        _sut.BiometricUnlockEnabled.Should().BeFalse();
        _sut.EnableBiometricUnlock.Should().BeFalse();
        _sut.CanConfigureBiometrics.Should().BeFalse();
        _sut.IsProcessing.Should().BeFalse();
        _sut.ErrorMessage.Should().Be("Unable to load app lock settings: Load failed");
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenServiceThrows_SetsErrorMessageAndClearsProcessingState()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));
        appLockService.VerifyPasswordAsync("Current#1", Arg.Any<CancellationToken>())
            .Returns(true);
        appLockService.ChangePasswordAsync("Changed#2", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Change failed")));

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var _sut = new AppLockSettingsViewModel(appLockService, biometricAuthService, _ => Task.CompletedTask);

        await _sut.InitializeAsync();
        _sut.CurrentPassword = "Current#1";
        _sut.ChangePassword = "Changed#2";
        _sut.ConfirmChangePassword = "Changed#2";

        // Act
        await _sut.ChangePasswordCommand.ExecuteAsync(null);

        // Assert
        _sut.IsProcessing.Should().BeFalse();
        _sut.ErrorMessage.Should().Be("Unable to update the app lock password: Change failed");
    }
}
