namespace BusLane.Tests.ViewModels;

using BusLane.Models.Security;
using BusLane.Services.Security;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public sealed class AppLockViewModelTests
{
    [Fact]
    public async Task InitializeAsync_WithUnavailableBiometrics_ShouldDisableBiometricUnlock()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: true));

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        biometricAuthService.GetAvailabilityAsync(Arg.Any<CancellationToken>())
            .Returns(BiometricAvailability.Unavailable);

        var sut = new AppLockViewModel(appLockService, biometricAuthService, () => Task.CompletedTask);

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.IsLocked.Should().BeTrue();
        sut.CanUseBiometrics.Should().BeFalse();
    }

    [Fact]
    public async Task RecoverWithNewPasswordAsync_WithValidRecoveryCode_ShouldUnlockAndChangePassword()
    {
        // Arrange
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));
        appLockService.VerifyRecoveryCodeAsync("ABCD-EFGH-IJKL-MNOP", Arg.Any<CancellationToken>())
            .Returns(true);

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var unlockCalls = 0;
        var sut = new AppLockViewModel(appLockService, biometricAuthService, () =>
        {
            unlockCalls++;
            return Task.CompletedTask;
        });

        await sut.InitializeAsync();
        sut.EnterRecoveryModeCommand.Execute(null);
        sut.RecoveryCode = "ABCD-EFGH-IJKL-MNOP";
        sut.NewPassword = "NewPassword#1";
        sut.ConfirmNewPassword = "NewPassword#1";

        // Act
        await sut.RecoverWithNewPasswordCommand.ExecuteAsync(null);

        // Assert
        await appLockService.Received(1).ChangePasswordAsync("NewPassword#1", Arg.Any<CancellationToken>());
        sut.IsLocked.Should().BeFalse();
        sut.IsRecoveryMode.Should().BeFalse();
        unlockCalls.Should().Be(1);
    }

    [Fact]
    public async Task UnlockAsync_AfterRepeatedFailures_ShouldApplyRetryBackoff()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var appLockService = Substitute.For<IAppLockService>();
        appLockService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new AppLockSnapshot(IsEnabled: true, BiometricUnlockEnabled: false));
        appLockService.VerifyPasswordAsync("Wrong#1", Arg.Any<CancellationToken>())
            .Returns(false);

        var biometricAuthService = Substitute.For<IBiometricAuthService>();
        var sut = new AppLockViewModel(
            appLockService,
            biometricAuthService,
            () => Task.CompletedTask,
            () => now);

        await sut.InitializeAsync();
        sut.Password = "Wrong#1";

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);
        sut.Password = "Wrong#1";
        await sut.UnlockCommand.ExecuteAsync(null);
        sut.Password = "Wrong#1";
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        sut.IsBackoffActive.Should().BeTrue();
        sut.ErrorMessage.Should().Be("Too many failed attempts. Try again in a few seconds.");

        // Act
        sut.Password = "Wrong#1";
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        await appLockService.Received(3).VerifyPasswordAsync("Wrong#1", Arg.Any<CancellationToken>());
    }
}
