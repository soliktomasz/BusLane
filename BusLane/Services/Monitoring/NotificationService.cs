namespace BusLane.Services.Monitoring;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class NotificationService : INotificationService
{

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
        // Pass AppleScript via stdin to avoid shell argument injection
        var escapedTitle = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        var script = $"display notification \"{escapedMessage}\" with title \"{escapedTitle}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        process.StandardInput.Write(script);
        process.StandardInput.Close();
    }

    private static void ShowWindowsNotification(string title, string message)
    {
        // XML-escape values to prevent injection in toast template
        var xmlTitle = System.Security.SecurityElement.Escape(
            title.Replace("\r", "").Replace("\n", " ")) ?? title;
        var xmlMessage = System.Security.SecurityElement.Escape(
            message.Replace("\r", "").Replace("\n", " ")) ?? message;

        var script = $@"
            [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
            [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
            $template = @'
            <toast>
                <visual>
                    <binding template=""ToastGeneric"">
                        <text>{xmlTitle}</text>
                        <text>{xmlMessage}</text>
                    </binding>
                </visual>
            </toast>
'@
            $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
            $xml.LoadXml($template)
            $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
            [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('BusLane').Show($toast)
        ";

        // Pass script via stdin to avoid command-line argument injection
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command -",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        process.StandardInput.Write(script);
        process.StandardInput.Close();
        process.WaitForExit(5000); // Wait max 5 seconds
    }

    private static void ShowLinuxNotification(string title, string message)
    {
        // Use ArgumentList to pass arguments without shell interpretation
        var psi = new ProcessStartInfo
        {
            FileName = "notify-send",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add(title);
        psi.ArgumentList.Add(message);

        var process = new Process { StartInfo = psi };
        process.Start();
        process.WaitForExit(2000); // Wait max 2 seconds
    }

    private void SaveSettings()
    {
        try
        {
            AppPaths.EnsureDirectoryExists();

            var settings = new NotificationSettings { IsEnabled = _isEnabled };
            var json = Serialize(settings);
            AppPaths.CreateSecureFile(AppPaths.NotificationSettings, json);
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
            if (File.Exists(AppPaths.NotificationSettings))
            {
                var json = File.ReadAllText(AppPaths.NotificationSettings);
                var settings = Deserialize<NotificationSettings>(json);
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

