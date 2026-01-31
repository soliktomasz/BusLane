namespace BusLane.Models.Update;

/// <summary>User preferences for auto-update behavior.</summary>
public record UpdatePreferences
{
    public DateTime? LastCheckTime { get; init; }
    public string? SkippedVersion { get; init; }
    public DateTime? RemindLaterDate { get; init; }
    public bool AutoCheckEnabled { get; init; } = true;
}
