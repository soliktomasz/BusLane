namespace BusLane.Services.Infrastructure;

/// <summary>
/// Centralized application paths for data storage.
/// Eliminates repeated path construction across services.
/// </summary>
internal static class AppPaths
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BusLane"
    );

    /// <summary>
    /// Path to the alert rules configuration file.
    /// </summary>
    public static string AlertRules => Path.Combine(AppDataFolder, "alert-rules.json");

    /// <summary>
    /// Path to the notification settings file.
    /// </summary>
    public static string NotificationSettings => Path.Combine(AppDataFolder, "notification-settings.json");

    /// <summary>
    /// Path to the user preferences file.
    /// </summary>
    public static string Preferences => Path.Combine(AppDataFolder, "preferences.json");

    /// <summary>
    /// Path to the saved connections file.
    /// </summary>
    public static string Connections => Path.Combine(AppDataFolder, "connections.json");

    /// <summary>
    /// Ensures the application data directory exists.
    /// </summary>
    public static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(AppDataFolder))
        {
            Directory.CreateDirectory(AppDataFolder);
        }
    }

    /// <summary>
    /// Gets the directory path for the application data folder.
    /// </summary>
    public static string DataFolder => AppDataFolder;
}

