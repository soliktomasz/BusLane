namespace BusLane.Services.Infrastructure;

using System.Text.Json;
using BusLane.Services.Abstractions;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

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
    public int MessagesPerPage { get; set; } = 100;
    public int MaxTotalMessages { get; set; } = 500;
    public bool ShowDeadLetterBadges { get; set; } = true;
    public bool EnableMessagePreview { get; set; } = true;
    public bool ShowNavigationPanel { get; set; } = true;
    public bool ShowTerminalPanel { get; set; }
    public bool TerminalIsDocked { get; set; } = true;
    public double TerminalDockHeight { get; set; } = 260;
    public string? TerminalWindowBoundsJson { get; set; }
    public string Theme { get; set; } = "Light";
    public int LiveStreamPollingIntervalSeconds { get; set; } = 1;

    // Session persistence
    public bool RestoreTabsOnStartup { get; set; } = true;
    public string OpenTabsJson { get; set; } = "[]";

    // Privacy
    public bool EnableTelemetry { get; set; }

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
                var data = Deserialize<PreferencesData>(json);
                if (data != null)
                {
                    ConfirmBeforeDelete = data.ConfirmBeforeDelete;
                    ConfirmBeforePurge = data.ConfirmBeforePurge;
                    AutoRefreshMessages = data.AutoRefreshMessages;
                    AutoRefreshIntervalSeconds = data.AutoRefreshIntervalSeconds;
                    DefaultMessageCount = data.DefaultMessageCount > 0 ? data.DefaultMessageCount : DefaultMessageCount;
                    MessagesPerPage = data.MessagesPerPage > 0 ? data.MessagesPerPage : MessagesPerPage;
                    MaxTotalMessages = data.MaxTotalMessages > 0 ? data.MaxTotalMessages : MaxTotalMessages;
                    ShowDeadLetterBadges = data.ShowDeadLetterBadges;
                    EnableMessagePreview = data.EnableMessagePreview;
                    ShowNavigationPanel = data.ShowNavigationPanel;
                    ShowTerminalPanel = data.ShowTerminalPanel;
                    TerminalIsDocked = data.TerminalIsDocked;
                    TerminalDockHeight = data.TerminalDockHeight > 0 ? data.TerminalDockHeight : TerminalDockHeight;
                    TerminalWindowBoundsJson = data.TerminalWindowBoundsJson;
                    Theme = data.Theme ?? "Light";
                    LiveStreamPollingIntervalSeconds = data.LiveStreamPollingIntervalSeconds;
                    RestoreTabsOnStartup = data.RestoreTabsOnStartup;
                    OpenTabsJson = data.OpenTabsJson ?? "[]";
                    EnableTelemetry = data.EnableTelemetry;
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
                MessagesPerPage = MessagesPerPage,
                MaxTotalMessages = MaxTotalMessages,
                ShowDeadLetterBadges = ShowDeadLetterBadges,
                EnableMessagePreview = EnableMessagePreview,
                ShowNavigationPanel = ShowNavigationPanel,
                ShowTerminalPanel = ShowTerminalPanel,
                TerminalIsDocked = TerminalIsDocked,
                TerminalDockHeight = TerminalDockHeight,
                TerminalWindowBoundsJson = TerminalWindowBoundsJson,
                Theme = Theme,
                LiveStreamPollingIntervalSeconds = LiveStreamPollingIntervalSeconds,
                RestoreTabsOnStartup = RestoreTabsOnStartup,
                OpenTabsJson = OpenTabsJson,
                EnableTelemetry = EnableTelemetry,
                AutoCheckForUpdates = AutoCheckForUpdates,
                SkippedUpdateVersion = SkippedUpdateVersion,
                UpdateRemindLaterDate = UpdateRemindLaterDate
            };

            var json = Serialize(data);
            AppPaths.CreateSecureFile(AppPaths.Preferences, json);
            
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
        public int MessagesPerPage { get; set; }
        public int MaxTotalMessages { get; set; }
        public bool ShowDeadLetterBadges { get; set; }
        public bool EnableMessagePreview { get; set; }
        public bool ShowNavigationPanel { get; set; } = true;
        public bool ShowTerminalPanel { get; set; }
        public bool TerminalIsDocked { get; set; } = true;
        public double TerminalDockHeight { get; set; } = 260;
        public string? TerminalWindowBoundsJson { get; set; }
        public string? Theme { get; set; }
        public int LiveStreamPollingIntervalSeconds { get; set; } = 1;
        public bool RestoreTabsOnStartup { get; set; } = true;
        public string? OpenTabsJson { get; set; }
        public bool EnableTelemetry { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }
    }
}
