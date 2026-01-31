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
}
