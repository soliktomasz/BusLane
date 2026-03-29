namespace BusLane.Models.Security;

/// <summary>
/// Represents the persisted app-lock state needed by the UI.
/// </summary>
public sealed record AppLockSnapshot(bool IsEnabled, bool BiometricUnlockEnabled)
{
    public static AppLockSnapshot Disabled { get; } = new(false, false);
}
