namespace BusLane.Services.Infrastructure;

/// <summary>
/// Service for checking and managing application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for available updates from GitHub Releases.
    /// </summary>
    /// <returns>True if an update is available, false otherwise.</returns>
    Task<bool> CheckForUpdatesAsync();

    /// <summary>
    /// Gets information about the latest release from GitHub.
    /// </summary>
    /// <returns>Release information including version, download URL, and release notes, or null if unavailable.</returns>
    Task<ReleaseInfo?> GetLatestReleaseInfoAsync();

    /// <summary>
    /// Gets whether an update is available based on the last check.
    /// </summary>
    bool IsUpdateAvailable { get; }

    /// <summary>
    /// Gets the latest release information from the last check.
    /// </summary>
    ReleaseInfo? LatestReleaseInfo { get; }

    /// <summary>
    /// Gets the timestamp of the last update check.
    /// </summary>
    DateTime? LastCheckTime { get; }
}

/// <summary>
/// Information about a GitHub release.
/// </summary>
public class ReleaseInfo
{
    public string Version { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public Dictionary<string, string> PlatformDownloadUrls { get; set; } = new();
    public DateTime PublishedAt { get; set; }
    public bool IsPrerelease { get; set; }
}
