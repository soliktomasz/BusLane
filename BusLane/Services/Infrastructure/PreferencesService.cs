using System.Text.Json;
using BusLane.Services.Abstractions;

namespace BusLane.Services.Infrastructure;

/// <summary>
/// Implementation of IPreferencesService that persists preferences to a JSON file.
/// </summary>
public class PreferencesService : IPreferencesService
{

    public bool ConfirmBeforeDelete { get; set; } = true;
    public bool ConfirmBeforePurge { get; set; } = true;
    public bool AutoRefreshMessages { get; set; }
    public int AutoRefreshIntervalSeconds { get; set; } = 30;
    public int DefaultMessageCount { get; set; } = 100;
    public bool ShowDeadLetterBadges { get; set; } = true;
    public bool EnableMessagePreview { get; set; } = true;
    public bool ShowNavigationPanel { get; set; } = true;
    public string Theme { get; set; } = "Light";
    public int LiveStreamPollingIntervalSeconds { get; set; } = 1;

    // Session persistence
    public bool RestoreTabsOnStartup { get; set; } = true;
    public string OpenTabsJson { get; set; } = "[]";

    // Update preferences
    public bool AutoCheckForUpdates { get; set; } = true;
    public string? SkippedUpdateVersion { get; set; }
    public DateTime? UpdateRemindLaterDate { get; set; }

    public event EventHandler? PreferencesChanged;

    public PreferencesService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(AppPaths.Preferences))
            {
                var json = File.ReadAllText(AppPaths.Preferences);
                var data = JsonSerializer.Deserialize<PreferencesData>(json);
                if (data != null)
                {
                    ConfirmBeforeDelete = data.ConfirmBeforeDelete;
                    ConfirmBeforePurge = data.ConfirmBeforePurge;
                    AutoRefreshMessages = data.AutoRefreshMessages;
                    AutoRefreshIntervalSeconds = data.AutoRefreshIntervalSeconds;
                    DefaultMessageCount = data.DefaultMessageCount;
                    ShowDeadLetterBadges = data.ShowDeadLetterBadges;
                    EnableMessagePreview = data.EnableMessagePreview;
                    ShowNavigationPanel = data.ShowNavigationPanel;
                    Theme = data.Theme ?? "Light";
                    LiveStreamPollingIntervalSeconds = data.LiveStreamPollingIntervalSeconds;
                    RestoreTabsOnStartup = data.RestoreTabsOnStartup;
                    OpenTabsJson = data.OpenTabsJson ?? "[]";
                    AutoCheckForUpdates = data.AutoCheckForUpdates;
                    SkippedUpdateVersion = data.SkippedUpdateVersion;
                    UpdateRemindLaterDate = data.UpdateRemindLaterDate;
                }
            }
        }
        catch
        {
            // Use defaults if loading fails
        }
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDirectoryExists();

            var data = new PreferencesData
            {
                ConfirmBeforeDelete = ConfirmBeforeDelete,
                ConfirmBeforePurge = ConfirmBeforePurge,
                AutoRefreshMessages = AutoRefreshMessages,
                AutoRefreshIntervalSeconds = AutoRefreshIntervalSeconds,
                DefaultMessageCount = DefaultMessageCount,
                ShowDeadLetterBadges = ShowDeadLetterBadges,
                EnableMessagePreview = EnableMessagePreview,
                ShowNavigationPanel = ShowNavigationPanel,
                Theme = Theme,
                LiveStreamPollingIntervalSeconds = LiveStreamPollingIntervalSeconds,
                RestoreTabsOnStartup = RestoreTabsOnStartup,
                OpenTabsJson = OpenTabsJson,
                AutoCheckForUpdates = AutoCheckForUpdates,
                SkippedUpdateVersion = SkippedUpdateVersion,
                UpdateRemindLaterDate = UpdateRemindLaterDate
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(AppPaths.Preferences, json);
            
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Silently fail if saving doesn't work
        }
    }

    private class PreferencesData
    {
        public bool ConfirmBeforeDelete { get; set; }
        public bool ConfirmBeforePurge { get; set; }
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; }
        public int DefaultMessageCount { get; set; }
        public bool ShowDeadLetterBadges { get; set; }
        public bool EnableMessagePreview { get; set; }
        public bool ShowNavigationPanel { get; set; } = true;
        public string? Theme { get; set; }
        public int LiveStreamPollingIntervalSeconds { get; set; } = 1;
        public bool RestoreTabsOnStartup { get; set; } = true;
        public string? OpenTabsJson { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }
    }
}
