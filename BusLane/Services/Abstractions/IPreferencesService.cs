namespace BusLane.Services.Abstractions;
/// <summary>
/// Interface for managing user preferences.
/// Provides testability and dependency injection support.
/// </summary>
public interface IPreferencesService
{
    bool ConfirmBeforeDelete { get; set; }
    bool ConfirmBeforePurge { get; set; }
    bool AutoRefreshMessages { get; set; }
    int AutoRefreshIntervalSeconds { get; set; }
    int DefaultMessageCount { get; set; }
    int MessagesPerPage { get; set; }
    int MaxTotalMessages { get; set; }
    bool ShowDeadLetterBadges { get; set; }
    bool EnableMessagePreview { get; set; }
    bool ShowNavigationPanel { get; set; }
    bool ShowTerminalPanel { get; set; }
    bool TerminalIsDocked { get; set; }
    double TerminalDockHeight { get; set; }
    string? TerminalWindowBoundsJson { get; set; }
    string Theme { get; set; }
    int LiveStreamPollingIntervalSeconds { get; set; }

    // Session persistence
    bool RestoreTabsOnStartup { get; set; }
    string OpenTabsJson { get; set; }

    // Privacy
    bool EnableTelemetry { get; set; }

    // Update preferences
    bool AutoCheckForUpdates { get; set; }
    string? SkippedUpdateVersion { get; set; }
    DateTime? UpdateRemindLaterDate { get; set; }

    void Save();
    void Load();
    event EventHandler? PreferencesChanged;
}
