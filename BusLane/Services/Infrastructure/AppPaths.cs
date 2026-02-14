namespace BusLane.Services.Infrastructure;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

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
    /// Creates a file with secure permissions (owner read/write only).
    /// Uses 0o600 permissions on Unix and restrictive ACLs on Windows.
    /// </summary>
    /// <param name="path">The full path to the file to create.</param>
    /// <param name="content">The content to write to the file.</param>
    public static void CreateSecureFile(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (OperatingSystem.IsWindows())
        {
            CreateSecureFileWindows(path, content);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            CreateSecureFileUnix(path, content);
        }
        else
        {
            // Fallback: create file without special permissions for unknown platforms
            File.WriteAllText(path, content);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void CreateSecureFileWindows(string path, string content)
    {
        // Create or overwrite the file
        File.WriteAllText(path, content);

        // Set restrictive ACL - only the current user has access
        var fileInfo = new FileInfo(path);
        var fileSecurity = fileInfo.GetAccessControl();

        // Remove all inherited permissions
        fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        // Add full control for the current user only
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser != null)
        {
            var userRule = new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                AccessControlType.Allow);
            fileSecurity.SetAccessRule(userRule);
        }

        // Also grant SYSTEM access if needed for backups, but deny others
        fileInfo.SetAccessControl(fileSecurity);
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macOS")]
    private static void CreateSecureFileUnix(string path, string content)
    {
        File.WriteAllText(path, content);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

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
    /// Path to the dashboard configuration file.
    /// </summary>
    public static string DashboardConfig => Path.Combine(AppDataFolder, "dashboard.json");

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
