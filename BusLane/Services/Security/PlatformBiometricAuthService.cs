namespace BusLane.Services.Security;

using BusLane.Models.Security;

public enum NativeBiometricAvailability
{
    Unavailable,
    NotEnrolled,
    Available
}

public enum NativeBiometricPromptResult
{
    Unavailable,
    Cancelled,
    Failed,
    Success
}

public interface IBiometricPromptAdapter
{
    Task<NativeBiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default);
    Task<NativeBiometricPromptResult> AuthenticateAsync(string reason, CancellationToken ct = default);
}

/// <summary>
/// Maps platform-native biometric results into the app's public contract.
/// </summary>
public abstract class PlatformBiometricAuthService : IBiometricAuthService
{
    private readonly IBiometricPromptAdapter _adapter;

    protected PlatformBiometricAuthService(IBiometricPromptAdapter adapter)
    {
        _adapter = adapter;
    }

    public async Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken ct = default)
    {
        var availability = await _adapter.GetAvailabilityAsync(ct);
        return availability == NativeBiometricAvailability.Available
            ? BiometricAvailability.Available
            : BiometricAvailability.Unavailable;
    }

    public async Task<BiometricAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        var result = await _adapter.AuthenticateAsync(reason, ct);
        return result switch
        {
            NativeBiometricPromptResult.Success => BiometricAuthResult.Success,
            NativeBiometricPromptResult.Cancelled => BiometricAuthResult.Cancelled,
            NativeBiometricPromptResult.Failed => BiometricAuthResult.Failed,
            _ => BiometricAuthResult.Unavailable
        };
    }
}
