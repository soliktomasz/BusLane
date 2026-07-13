namespace BusLane.Services.Infrastructure;

using System.Text.Json;
using BusLane.Services.Abstractions;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

/// <summary>
/// Implementation of IPreferencesService that persists preferences to a JSON file.
/// </summary>
public class PreferencesService : IPreferencesService
{
    private readonly string _preferencesPath;

    public bool ConfirmBeforeDelete { get; set; } = true;
    public bool ConfirmBeforePurge { get; set; } = true;
    public bool AutoRefreshMessages { get; set; }
    public int AutoRefreshIntervalSeconds { get; set; } = 30;
    public int DefaultMessageCount { get; set; } = 100;
    public int MessagesPerPage { get; set; } = 100;
    public bool ShowDeadLetterBadges { get; set; } = true;
    public bool ShowTopicActionButtons { get; set; } = true;
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
    public string PinnedEntitiesJson { get; set; } = "[]";
    public bool HasSeenIntroduction { get; set; }

    // Privacy
    public bool EnableTelemetry { get; set; }

    // Update preferences
    public bool AutoCheckForUpdates { get; set; } = true;
    public string? SkippedUpdateVersion { get; set; }
    public DateTime? UpdateRemindLaterDate { get; set; }

    public event EventHandler? PreferencesChanged;

    public PreferencesService(string? preferencesPath = null)
    {
        _preferencesPath = preferencesPath ?? AppPaths.Preferences;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_preferencesPath))
            {
                var json = File.ReadAllText(_preferencesPath);
                var data = Deserialize<PreferencesData>(json);
                if (data != null)
                {
                    ConfirmBeforeDelete = data.ConfirmBeforeDelete;
                    ConfirmBeforePurge = data.ConfirmBeforePurge;
                    AutoRefreshMessages = data.AutoRefreshMessages;
                    AutoRefreshIntervalSeconds = data.AutoRefreshIntervalSeconds;
                    DefaultMessageCount = data.DefaultMessageCount > 0 ? data.DefaultMessageCount : DefaultMessageCount;
                    MessagesPerPage = data.MessagesPerPage > 0 ? data.MessagesPerPage : MessagesPerPage;
                    ShowDeadLetterBadges = data.ShowDeadLetterBadges;
                    ShowTopicActionButtons = data.ShowTopicActionButtons;
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
                    PinnedEntitiesJson = data.PinnedEntitiesJson ?? "[]";
                    HasSeenIntroduction = data.HasSeenIntroduction;
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
            var directory = Path.GetDirectoryName(_preferencesPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = new PreferencesData
            {
                ConfirmBeforeDelete = ConfirmBeforeDelete,
                ConfirmBeforePurge = ConfirmBeforePurge,
                AutoRefreshMessages = AutoRefreshMessages,
                AutoRefreshIntervalSeconds = AutoRefreshIntervalSeconds,
                DefaultMessageCount = DefaultMessageCount,
                MessagesPerPage = MessagesPerPage,
                ShowDeadLetterBadges = ShowDeadLetterBadges,
                ShowTopicActionButtons = ShowTopicActionButtons,
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
                PinnedEntitiesJson = PinnedEntitiesJson,
                HasSeenIntroduction = HasSeenIntroduction,
                EnableTelemetry = EnableTelemetry,
                AutoCheckForUpdates = AutoCheckForUpdates,
                SkippedUpdateVersion = SkippedUpdateVersion,
                UpdateRemindLaterDate = UpdateRemindLaterDate
            };

            var json = Serialize(data);
            AppPaths.CreateSecureFile(_preferencesPath, json);
            
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
        public bool ShowDeadLetterBadges { get; set; }
        public bool ShowTopicActionButtons { get; set; } = true;
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
        public string? PinnedEntitiesJson { get; set; }
        public bool HasSeenIntroduction { get; set; }
        public bool EnableTelemetry { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }
    }
}
