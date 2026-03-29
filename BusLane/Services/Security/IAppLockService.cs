namespace BusLane.Services.Security;

using BusLane.Models.Security;

/// <summary>
/// Manages the opt-in application lock state.
/// </summary>
public interface IAppLockService
{
    Task<AppLockSnapshot> GetSnapshotAsync(CancellationToken ct = default);
    Task<bool> VerifyPasswordAsync(string password, CancellationToken ct = default);
    Task<string> EnableAsync(AppLockConfiguration configuration, CancellationToken ct = default);
    Task DisableAsync(CancellationToken ct = default);
    Task ChangePasswordAsync(string newPassword, CancellationToken ct = default);
    Task<bool> VerifyRecoveryCodeAsync(string recoveryCode, CancellationToken ct = default);
    Task<string> RegenerateRecoveryCodeAsync(CancellationToken ct = default);
    Task UpdateBiometricPreferenceAsync(bool enabled, CancellationToken ct = default);
}
