namespace BusLane.Services.Security;

using BusLane.Models.Security;

/// <summary>
/// Provides biometric availability checks and authentication prompts.
/// </summary>
public interface IBiometricAuthService
{
    Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default);
    Task<BiometricAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default);
}
