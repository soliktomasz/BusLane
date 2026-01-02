namespace BusLane.Services.Infrastructure;

/// <summary>
/// Service for retrieving application version information.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the application version (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the full version string including any suffix (e.g., "1.0.0-beta.1").
    /// </summary>
    string InformationalVersion { get; }

    /// <summary>
    /// Gets the product name.
    /// </summary>
    string ProductName { get; }

    /// <summary>
    /// Gets the copyright information.
    /// </summary>
    string Copyright { get; }

    /// <summary>
    /// Gets a display string suitable for showing in the UI (e.g., "v1.0.0").
    /// </summary>
    string DisplayVersion { get; }
}

