namespace BusLane.Services.Dashboard;

using Models;

/// <summary>
/// Persists and loads dashboard widget configurations to/from disk.
/// </summary>
public interface IDashboardPersistenceService
{
    /// <summary>Loads the dashboard configuration from disk, or returns defaults if not found or corrupted.</summary>
    DashboardConfiguration Load();

    /// <summary>Saves the dashboard configuration to disk.</summary>
    void Save(DashboardConfiguration config);

    /// <summary>Returns the default dashboard configuration with preset widgets.</summary>
    DashboardConfiguration GetDefaultConfiguration();

    /// <summary>Gets all saved dashboard presets.</summary>
    IReadOnlyList<DashboardPreset> GetPresets();

    /// <summary>Saves or updates a named dashboard preset.</summary>
    void SavePreset(DashboardPreset preset);

    /// <summary>Loads a preset by its identifier.</summary>
    DashboardPreset? LoadPreset(string presetId);
}
