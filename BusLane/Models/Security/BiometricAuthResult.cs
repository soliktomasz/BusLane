namespace BusLane.Models.Security;

/// <summary>
/// Represents the outcome of a biometric prompt.
/// </summary>
public enum BiometricAuthResult
{
    Unavailable,
    Cancelled,
    Failed,
    Success
}
