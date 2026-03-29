namespace BusLane.ViewModels;

using BusLane.Models.Security;
using BusLane.Services.Security;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Coordinates application unlock state at startup.
/// </summary>
public partial class AppLockViewModel : ViewModelBase
{
    private const int FailedAttemptsBeforeBackoff = 3;
    private static readonly TimeSpan RetryBackoffDuration = TimeSpan.FromSeconds(5);

    private readonly IAppLockService _appLockService;
    private readonly IBiometricAuthService _biometricAuthService;
    private readonly Func<Task> _onUnlockSucceeded;
    private readonly Func<DateTimeOffset> _utcNow;
    private bool _sessionUnlocked;
    private int _failedAttempts;
    private DateTimeOffset? _backoffUntilUtc;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private bool _isUnlocking;
    [ObservableProperty] private bool _isRecoveryMode;
    [ObservableProperty] private bool _biometricUnlockEnabled;
    [ObservableProperty] private BiometricAvailability _biometricAvailability = BiometricAvailability.Unavailable;
    [ObservableProperty] private bool _isBackoffActive;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _recoveryCode = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmNewPassword = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public bool CanUseBiometrics => BiometricUnlockEnabled && BiometricAvailability == BiometricAvailability.Available;

    public AppLockViewModel(
        IAppLockService appLockService,
        IBiometricAuthService biometricAuthService,
        Func<Task> onUnlockSucceeded,
        Func<DateTimeOffset>? utcNow = null)
    {
        _appLockService = appLockService;
        _biometricAuthService = biometricAuthService;
        _onUnlockSucceeded = onUnlockSucceeded;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var snapshot = await _appLockService.GetSnapshotAsync(ct);
        await ApplySnapshotAsync(snapshot, trustCurrentSession: false, ct);
    }

    public async Task ApplySettingsSnapshotAsync(AppLockSnapshot snapshot, CancellationToken ct = default)
    {
        await ApplySnapshotAsync(snapshot, trustCurrentSession: true, ct);
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (!IsLocked || IsUnlocking)
        {
            return;
        }

        if (!EnsureUnlockAvailable())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your app lock password.";
            return;
        }

        IsUnlocking = true;
        try
        {
            if (await _appLockService.VerifyPasswordAsync(Password))
            {
                await CompleteUnlockAsync();
            }
            else
            {
                Password = string.Empty;
                RegisterFailedAttempt("Incorrect password.");
            }
        }
        finally
        {
            IsUnlocking = false;
        }
    }

    [RelayCommand]
    private async Task UnlockWithBiometricsAsync()
    {
        if (!IsLocked || IsUnlocking || !CanUseBiometrics)
        {
            return;
        }

        if (!EnsureUnlockAvailable())
        {
            return;
        }

        IsUnlocking = true;
        try
        {
            var result = await _biometricAuthService.AuthenticateAsync("unlock BusLane");
            switch (result)
            {
                case BiometricAuthResult.Success:
                    await CompleteUnlockAsync();
                    break;
                case BiometricAuthResult.Cancelled:
                    ErrorMessage = null;
                    break;
                case BiometricAuthResult.Failed:
                    RegisterFailedAttempt("Biometric authentication failed.");
                    break;
                default:
                    BiometricAvailability = BiometricAvailability.Unavailable;
                    ErrorMessage = "Biometric unlock is unavailable.";
                    break;
            }
        }
        finally
        {
            IsUnlocking = false;
        }
    }

    [RelayCommand]
    private void EnterRecoveryMode()
    {
        IsRecoveryMode = true;
        ErrorMessage = null;
        Password = string.Empty;
    }

    [RelayCommand]
    private void ExitRecoveryMode()
    {
        IsRecoveryMode = false;
        ErrorMessage = null;
        RecoveryCode = string.Empty;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
    }

    [RelayCommand]
    private async Task RecoverWithNewPasswordAsync()
    {
        if (!EnsureUnlockAvailable())
        {
            return;
        }

        if (!await VerifyRecoveryCodeAsync())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Enter a new password.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        await _appLockService.ChangePasswordAsync(NewPassword);
        IsRecoveryMode = false;
        await CompleteUnlockAsync();
    }

    [RelayCommand]
    private async Task DisableLockWithRecoveryCodeAsync()
    {
        if (!EnsureUnlockAvailable())
        {
            return;
        }

        if (!await VerifyRecoveryCodeAsync())
        {
            return;
        }

        await _appLockService.DisableAsync();
        IsEnabled = false;
        BiometricUnlockEnabled = false;
        BiometricAvailability = BiometricAvailability.Unavailable;
        OnPropertyChanged(nameof(CanUseBiometrics));
        IsRecoveryMode = false;
        await CompleteUnlockAsync();
    }

    private async Task<bool> VerifyRecoveryCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(RecoveryCode))
        {
            ErrorMessage = "Enter your recovery code.";
            return false;
        }

        var isValid = await _appLockService.VerifyRecoveryCodeAsync(RecoveryCode);
        if (!isValid)
        {
            RegisterFailedAttempt("Recovery code is incorrect.");
            return false;
        }

        ResetFailedAttempts();
        return true;
    }

    private async Task CompleteUnlockAsync()
    {
        _sessionUnlocked = true;
        IsLocked = false;
        ErrorMessage = null;
        ResetFailedAttempts();
        ClearSensitiveFields();
        await _onUnlockSucceeded();
    }

    partial void OnBiometricUnlockEnabledChanged(bool value) => OnPropertyChanged(nameof(CanUseBiometrics));
    partial void OnBiometricAvailabilityChanged(BiometricAvailability value) => OnPropertyChanged(nameof(CanUseBiometrics));

    private void ClearSensitiveFields()
    {
        Password = string.Empty;
        RecoveryCode = string.Empty;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
    }

    private async Task ApplySnapshotAsync(AppLockSnapshot snapshot, bool trustCurrentSession, CancellationToken ct)
    {
        if (!snapshot.IsEnabled)
        {
            _sessionUnlocked = false;
            ResetFailedAttempts();
        }
        else if (trustCurrentSession)
        {
            _sessionUnlocked = true;
        }

        IsEnabled = snapshot.IsEnabled;
        BiometricUnlockEnabled = snapshot.BiometricUnlockEnabled;
        BiometricAvailability = snapshot.IsEnabled && snapshot.BiometricUnlockEnabled
            ? await _biometricAuthService.GetAvailabilityAsync(ct)
            : BiometricAvailability.Unavailable;
        IsLocked = snapshot.IsEnabled && !_sessionUnlocked;
        IsRecoveryMode = false;
        ErrorMessage = null;
        ClearSensitiveFields();
    }

    private bool EnsureUnlockAvailable()
    {
        if (_backoffUntilUtc.HasValue && _utcNow() >= _backoffUntilUtc.Value)
        {
            ResetFailedAttempts();
        }

        if (_backoffUntilUtc.HasValue)
        {
            IsBackoffActive = true;
            ErrorMessage = "Too many failed attempts. Try again in a few seconds.";
            return false;
        }

        return true;
    }

    private void RegisterFailedAttempt(string message)
    {
        _failedAttempts++;
        if (_failedAttempts >= FailedAttemptsBeforeBackoff)
        {
            _backoffUntilUtc = _utcNow().Add(RetryBackoffDuration);
            IsBackoffActive = true;
            _failedAttempts = 0;
            ErrorMessage = "Too many failed attempts. Try again in a few seconds.";
            return;
        }

        ErrorMessage = message;
    }

    private void ResetFailedAttempts()
    {
        _failedAttempts = 0;
        _backoffUntilUtc = null;
        IsBackoffActive = false;
    }
}
