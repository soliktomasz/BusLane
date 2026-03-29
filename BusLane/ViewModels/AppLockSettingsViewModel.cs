namespace BusLane.ViewModels;

using BusLane.Models.Security;
using BusLane.Services.Security;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Handles security settings for the opt-in app lock.
/// </summary>
public partial class AppLockSettingsViewModel : ViewModelBase
{
    private readonly IAppLockService _appLockService;
    private readonly IBiometricAuthService _biometricAuthService;
    private readonly Func<AppLockSnapshot, Task> _applyRuntimeSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEnableSection))]
    [NotifyPropertyChangedFor(nameof(ShowManageSection))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfigureBiometrics))]
    private BiometricAvailability _biometricAvailability = BiometricAvailability.Unavailable;

    [ObservableProperty] private bool _biometricUnlockEnabled;
    [ObservableProperty] private bool _enableBiometricUnlock;
    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _changePassword = string.Empty;
    [ObservableProperty] private string _confirmChangePassword = string.Empty;
    [ObservableProperty] private bool _hasStoredRecoveryCode;
    [ObservableProperty] private string? _recoveryCode;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isProcessing;

    public bool CanConfigureBiometrics => BiometricAvailability == BiometricAvailability.Available;
    public bool ShowEnableSection => !IsEnabled;
    public bool ShowManageSection => IsEnabled;

    public AppLockSettingsViewModel(
        IAppLockService appLockService,
        IBiometricAuthService biometricAuthService,
        Func<AppLockSnapshot, Task> applyRuntimeSnapshot)
    {
        _appLockService = appLockService;
        _biometricAuthService = biometricAuthService;
        _applyRuntimeSnapshot = applyRuntimeSnapshot;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var snapshot = await _appLockService.GetSnapshotAsync(ct);
        IsEnabled = snapshot.IsEnabled;
        BiometricUnlockEnabled = snapshot.BiometricUnlockEnabled;
        EnableBiometricUnlock = snapshot.BiometricUnlockEnabled;
        HasStoredRecoveryCode = false;
        BiometricAvailability = await _biometricAuthService.GetAvailabilityAsync(ct);
        ErrorMessage = null;
        StatusMessage = null;
        RecoveryCode = null;
        ClearInputFields();
    }

    [RelayCommand]
    private async Task EnableAppLockAsync()
    {
        if (IsProcessing)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Enter a password for app lock.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        if (!HasStoredRecoveryCode)
        {
            ErrorMessage = "Confirm that you will store the recovery code before enabling app lock.";
            return;
        }

        if (EnableBiometricUnlock && !CanConfigureBiometrics)
        {
            ErrorMessage = "Biometric unlock is unavailable on this device.";
            return;
        }

        IsProcessing = true;
        try
        {
            RecoveryCode = await _appLockService.EnableAsync(new AppLockConfiguration(NewPassword, EnableBiometricUnlock));
            IsEnabled = true;
            BiometricUnlockEnabled = EnableBiometricUnlock;
            StatusMessage = "App lock enabled. Store the recovery code securely.";
            await _applyRuntimeSnapshot(new AppLockSnapshot(true, BiometricUnlockEnabled));
            ClearInputFields();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (IsProcessing)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(ChangePassword))
        {
            ErrorMessage = "Enter a new password.";
            return;
        }

        if (!string.Equals(ChangePassword, ConfirmChangePassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        if (!await ReauthenticateAsync())
        {
            return;
        }

        IsProcessing = true;
        try
        {
            await _appLockService.ChangePasswordAsync(ChangePassword);
            StatusMessage = "App lock password updated.";
            ChangePassword = string.Empty;
            ConfirmChangePassword = string.Empty;
            CurrentPassword = string.Empty;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task DisableAppLockAsync()
    {
        if (IsProcessing)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;

        if (!await ReauthenticateAsync())
        {
            return;
        }

        IsProcessing = true;
        try
        {
            await _appLockService.DisableAsync();
            IsEnabled = false;
            BiometricUnlockEnabled = false;
            EnableBiometricUnlock = false;
            RecoveryCode = null;
            HasStoredRecoveryCode = false;
            StatusMessage = "App lock disabled for future launches.";
            await _applyRuntimeSnapshot(AppLockSnapshot.Disabled);
            ClearInputFields();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task RegenerateRecoveryCodeAsync()
    {
        if (IsProcessing)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;

        if (!await ReauthenticateAsync())
        {
            return;
        }

        IsProcessing = true;
        try
        {
            RecoveryCode = await _appLockService.RegenerateRecoveryCodeAsync();
            StatusMessage = "Recovery code regenerated. Store the new code securely.";
            CurrentPassword = string.Empty;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ToggleBiometricUnlockAsync(bool enabled)
    {
        if (IsProcessing)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;

        if (enabled && !CanConfigureBiometrics)
        {
            ErrorMessage = "Biometric unlock is unavailable on this device.";
            return;
        }

        if (!await ReauthenticateAsync())
        {
            return;
        }

        IsProcessing = true;
        try
        {
            await _appLockService.UpdateBiometricPreferenceAsync(enabled);
            BiometricUnlockEnabled = enabled;
            EnableBiometricUnlock = enabled;
            StatusMessage = enabled
                ? "Biometric unlock enabled."
                : "Biometric unlock disabled.";
            await _applyRuntimeSnapshot(new AppLockSnapshot(IsEnabled, BiometricUnlockEnabled));
            CurrentPassword = string.Empty;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private Task EnableBiometricUnlockAsync() => ToggleBiometricUnlockAsync(true);

    [RelayCommand]
    private Task DisableBiometricUnlockAsync() => ToggleBiometricUnlockAsync(false);

    [RelayCommand]
    private void ClearRecoveryCode()
    {
        RecoveryCode = null;
        StatusMessage = null;
    }

    private async Task<bool> ReauthenticateAsync()
    {
        if (!string.IsNullOrWhiteSpace(CurrentPassword))
        {
            var isValid = await _appLockService.VerifyPasswordAsync(CurrentPassword);
            if (!isValid)
            {
                ErrorMessage = "Current password is incorrect.";
                return false;
            }

            return true;
        }

        if (!CanConfigureBiometrics)
        {
            ErrorMessage = "Enter your current password to continue.";
            return false;
        }

        var result = await _biometricAuthService.AuthenticateAsync("confirm BusLane security changes");
        switch (result)
        {
            case BiometricAuthResult.Success:
                return true;
            case BiometricAuthResult.Cancelled:
                ErrorMessage = null;
                return false;
            case BiometricAuthResult.Failed:
                ErrorMessage = "Biometric authentication failed.";
                return false;
            default:
                ErrorMessage = "Biometric unlock is unavailable on this device.";
                return false;
        }
    }

    private void ClearInputFields()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ChangePassword = string.Empty;
        ConfirmChangePassword = string.Empty;
        HasStoredRecoveryCode = false;
    }
}
