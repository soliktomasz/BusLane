namespace BusLane.Services.Security;

using BusLane.Models.Security;

/// <summary>
/// Fallback biometric service used when the host platform has no biometric support.
/// </summary>
public sealed class NoOpBiometricAuthService : IBiometricAuthService
{
    public Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default)
    {
        return Task.FromResult(BiometricAvailability.Unavailable);
    }

    public Task<BiometricAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        return Task.FromResult(BiometricAuthResult.Unavailable);
    }
}
