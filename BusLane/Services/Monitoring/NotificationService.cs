namespace BusLane.Services.Monitoring;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BusLane.Models;

public class NotificationService : INotificationService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BusLane",
        "notification-settings.json"
    );

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            SaveSettings();
        }
    }

    public NotificationService()
    {
        LoadSettings();
    }

    public void ShowAlertNotification(AlertEvent alert)
    {
        if (!IsEnabled)
            return;

        var title = $"BusLane Alert: {alert.Rule.Name}";
        var message = $"{alert.EntityType}: {alert.EntityName}\n" +
                      $"Current value: {alert.CurrentValue:N0} (threshold: {alert.Rule.Threshold:N0})";

        var type = alert.Rule.Severity switch
        {
            AlertSeverity.Critical => NotificationType.Error,
            AlertSeverity.Warning => NotificationType.Warning,
            _ => NotificationType.Info
        };

        ShowNotification(title, message, type);
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        if (!IsEnabled)
            return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ShowMacNotification(title, message);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ShowWindowsNotification(title, message);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ShowLinuxNotification(title, message);
            }
        }
        catch
        {
            // Silently fail if notifications don't work
        }
    }

    private static void ShowMacNotification(string title, string message)
    {
        // Use osascript to display notification on macOS
        var escapedTitle = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        var script = $"display notification \"{escapedMessage}\" with title \"{escapedTitle}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = true,
                CreateNoWindow = true
            }
        };

        process.Start();
    }

    private static void ShowWindowsNotification(string title, string message)
    {
        // Use PowerShell to display Windows toast notification
        var escapedTitle = title.Replace("'", "''");
        var escapedMessage = message.Replace("'", "''").Replace("\n", "`n");

        var script = $@"
            [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
            [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
            $template = @'
            <toast>
                <visual>
                    <binding template=""ToastGeneric"">
                        <text>{escapedTitle}</text>
                        <text>{escapedMessage}</text>
                    </binding>
                </visual>
            </toast>
'@
            $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
            $xml.LoadXml($template)
            $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
            [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('BusLane').Show($toast)
        ";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        process.WaitForExit(5000); // Wait max 5 seconds
    }

    private static void ShowLinuxNotification(string title, string message)
    {
        // Use notify-send for Linux notifications
        var escapedTitle = title.Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\"", "\\\"");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = $"\"{escapedTitle}\" \"{escapedMessage}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        process.WaitForExit(2000); // Wait max 2 seconds
    }

    private void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new NotificationSettings { IsEnabled = _isEnabled };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<NotificationSettings>(json);
                _isEnabled = settings?.IsEnabled ?? false;
            }
        }
        catch
        {
            _isEnabled = false;
        }
    }

    private class NotificationSettings
    {
        public bool IsEnabled { get; set; }
    }
}

