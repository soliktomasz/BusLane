namespace BusLane.Models.Update;

/// <summary>Represents the current state of the update process.</summary>
public enum UpdateStatus
{
    Idle,
    Checking,
    UpdateAvailable,
    Downloading,
    Downloaded,
    Installing,
    Error
}
