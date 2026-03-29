namespace BusLane.Services.Security;

using BusLane.Models.Security;

/// <summary>
/// Provides biometric availability checks and authentication prompts.
/// </summary>
public interface IBiometricAuthService
{
    /// <summary>
    /// Checks whether biometric authentication is available for BusLane on the current host.
    /// </summary>
    /// <param name="ct">Cancels the availability check.</param>
    /// <returns>A value describing whether biometric authentication can be used.</returns>
    Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Shows a biometric authentication prompt for the current user.
    /// </summary>
    /// <param name="reason">A user-facing reason describing why BusLane is requesting biometric authentication.</param>
    /// <param name="ct">Cancels the authentication operation.</param>
    /// <returns>The biometric prompt result for the attempted authentication.</returns>
    Task<BiometricAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default);
}
