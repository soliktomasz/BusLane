namespace BusLane.Models.Update;

/// <summary>Represents the current state of the update process.</summary>
public enum UpdateStatus
{
    Idle,
    NotInstalled,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToRestart,
    Installing,
    Error
}
