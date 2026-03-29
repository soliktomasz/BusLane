namespace BusLane.Models.Security;

/// <summary>
/// Represents the app-lock configuration being applied by the user.
/// </summary>
public sealed record AppLockConfiguration(string Password, bool BiometricUnlockEnabled);
