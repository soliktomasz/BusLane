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
    bool ShowDeadLetterBadges { get; set; }
    bool EnableMessagePreview { get; set; }
    bool ShowNavigationPanel { get; set; }
    string Theme { get; set; }
    void Save();
    void Load();
    event EventHandler? PreferencesChanged;
}
