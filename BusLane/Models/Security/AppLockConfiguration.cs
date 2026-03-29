namespace BusLane.Models.Security;

/// <summary>
/// Represents the app-lock configuration being applied by the user.
/// </summary>
public sealed record AppLockConfiguration(string Password, bool BiometricUnlockEnabled)
{
    public override string ToString()
    {
        return $"AppLockConfiguration {{ Password = <redacted>, BiometricUnlockEnabled = {BiometricUnlockEnabled} }}";
    }
}
