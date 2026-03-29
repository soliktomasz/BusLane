namespace BusLane.Tests.Services.Security;

using BusLane.Models.Security;
using BusLane.Services.Security;
using FluentAssertions;

public sealed class AppLockServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _lockFilePath;
    private readonly AppLockService _sut;

    public AppLockServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "BusLane.Tests", Guid.NewGuid().ToString("N"));
        _lockFilePath = Path.Combine(_tempDirectory, "app-lock.json");
        _sut = new AppLockService(_lockFilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }

    [Fact]
    public async Task EnableAsync_WithPassword_PersistsEnabledLockAndVerifiesPassword()
    {
        // Arrange
        var configuration = new AppLockConfiguration("S3cure-Passphrase!", true);

        // Act
        var recoveryCode = await _sut.EnableAsync(configuration);
        var snapshot = await _sut.GetSnapshotAsync();
        var validPassword = await _sut.VerifyPasswordAsync("S3cure-Passphrase!");
        var invalidPassword = await _sut.VerifyPasswordAsync("wrong-password");
        var validRecoveryCode = await _sut.VerifyRecoveryCodeAsync(recoveryCode);

        // Assert
        recoveryCode.Should().NotBeNullOrWhiteSpace();
        snapshot.IsEnabled.Should().BeTrue();
        snapshot.BiometricUnlockEnabled.Should().BeTrue();
        validPassword.Should().BeTrue();
        invalidPassword.Should().BeFalse();
        validRecoveryCode.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_AfterEnable_ReplacesPreviousPassword()
    {
        // Arrange
        await _sut.EnableAsync(new AppLockConfiguration("OldPassword#1", false));

        // Act
        await _sut.ChangePasswordAsync("NewPassword#2");
        var oldPasswordValid = await _sut.VerifyPasswordAsync("OldPassword#1");
        var newPasswordValid = await _sut.VerifyPasswordAsync("NewPassword#2");

        // Assert
        oldPasswordValid.Should().BeFalse();
        newPasswordValid.Should().BeTrue();
    }

    [Fact]
    public async Task DisableAsync_AfterEnable_ClearsLockState()
    {
        // Arrange
        await _sut.EnableAsync(new AppLockConfiguration("Password#1", true));

        // Act
        await _sut.DisableAsync();
        var snapshot = await _sut.GetSnapshotAsync();
        var validPassword = await _sut.VerifyPasswordAsync("Password#1");

        // Assert
        snapshot.IsEnabled.Should().BeFalse();
        snapshot.BiometricUnlockEnabled.Should().BeFalse();
        validPassword.Should().BeFalse();
        File.Exists(_lockFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task RegenerateRecoveryCodeAsync_AfterEnable_InvalidatesPreviousRecoveryCode()
    {
        // Arrange
        var originalRecoveryCode = await _sut.EnableAsync(new AppLockConfiguration("Password#1", false));

        // Act
        var replacementRecoveryCode = await _sut.RegenerateRecoveryCodeAsync();
        var originalCodeValid = await _sut.VerifyRecoveryCodeAsync(originalRecoveryCode);
        var replacementCodeValid = await _sut.VerifyRecoveryCodeAsync(replacementRecoveryCode);

        // Assert
        replacementRecoveryCode.Should().NotBe(originalRecoveryCode);
        originalCodeValid.Should().BeFalse();
        replacementCodeValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetSnapshotAsync_WithMissingFile_ReturnsDisabledSnapshot()
    {
        // Act
        var snapshot = await _sut.GetSnapshotAsync();

        // Assert
        snapshot.IsEnabled.Should().BeFalse();
        snapshot.BiometricUnlockEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSnapshotAsync_WithCorruptedFile_ReturnsDisabledSnapshot()
    {
        // Arrange
        Directory.CreateDirectory(_tempDirectory);
        await File.WriteAllTextAsync(_lockFilePath, "{not-valid-json");

        // Act
        var snapshot = await _sut.GetSnapshotAsync();
        var validPassword = await _sut.VerifyPasswordAsync("Password#1");

        // Assert
        snapshot.IsEnabled.Should().BeFalse();
        snapshot.BiometricUnlockEnabled.Should().BeFalse();
        validPassword.Should().BeFalse();
    }

    [Fact]
    public async Task EnableAsync_PersistsHashedSecretsWithoutPlaintextValues()
    {
        // Arrange
        const string password = "S3cure-Passphrase!";

        // Act
        var recoveryCode = await _sut.EnableAsync(new AppLockConfiguration(password, false));
        var persistedJson = await File.ReadAllTextAsync(_lockFilePath);

        // Assert
        persistedJson.Should().NotContain(password);
        persistedJson.Should().NotContain(recoveryCode);
        persistedJson.Should().Contain("\"version\"");

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.GetUnixFileMode(_lockFilePath)
                .Should()
                .Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
