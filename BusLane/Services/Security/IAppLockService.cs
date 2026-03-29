namespace BusLane.Services.Security;

using BusLane.Models.Security;

/// <summary>
/// Manages the opt-in application lock state.
/// </summary>
public interface IAppLockService
{
    /// <summary>
    /// Loads the current app-lock snapshot used by the UI.
    /// </summary>
    /// <param name="ct">Cancels the load operation.</param>
    /// <returns>
    /// A snapshot describing whether app lock is enabled and whether biometric unlock is enabled.
    /// Returns <see cref="AppLockSnapshot.Disabled"/> when no valid app-lock state exists.
    /// </returns>
    Task<AppLockSnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Verifies an app-lock password against the persisted hash.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="ct">Cancels the verification operation.</param>
    /// <returns><see langword="true"/> when the password matches the persisted app-lock state; otherwise, <see langword="false"/>.</returns>
    Task<bool> VerifyPasswordAsync(string password, CancellationToken ct = default);

    /// <summary>
    /// Enables app lock with the supplied configuration and generates a recovery code.
    /// </summary>
    /// <param name="configuration">The app-lock configuration to persist.</param>
    /// <param name="ct">Cancels the enable operation.</param>
    /// <returns>The newly generated recovery code. The plaintext value is only returned from this call.</returns>
    Task<string> EnableAsync(AppLockConfiguration configuration, CancellationToken ct = default);

    /// <summary>
    /// Disables app lock for future launches.
    /// </summary>
    /// <param name="ct">Cancels the disable operation.</param>
    Task DisableAsync(CancellationToken ct = default);

    /// <summary>
    /// Replaces the current app-lock password with a new value.
    /// </summary>
    /// <param name="newPassword">The new plaintext password to hash and persist.</param>
    /// <param name="ct">Cancels the password change operation.</param>
    Task ChangePasswordAsync(string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Verifies a recovery code against the persisted hash.
    /// </summary>
    /// <param name="recoveryCode">The plaintext recovery code to verify.</param>
    /// <param name="ct">Cancels the verification operation.</param>
    /// <returns><see langword="true"/> when the recovery code matches the persisted app-lock state; otherwise, <see langword="false"/>.</returns>
    Task<bool> VerifyRecoveryCodeAsync(string recoveryCode, CancellationToken ct = default);

    /// <summary>
    /// Regenerates the recovery code for the current app-lock state.
    /// </summary>
    /// <param name="ct">Cancels the regeneration operation.</param>
    /// <returns>The newly generated recovery code. The plaintext value is only returned from this call.</returns>
    Task<string> RegenerateRecoveryCodeAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the persisted biometric unlock preference.
    /// </summary>
    /// <param name="enabled">Whether biometric unlock should be enabled for future launches.</param>
    /// <param name="ct">Cancels the update operation.</param>
    Task UpdateBiometricPreferenceAsync(bool enabled, CancellationToken ct = default);
}
