namespace BusLane.Models.Update;

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
