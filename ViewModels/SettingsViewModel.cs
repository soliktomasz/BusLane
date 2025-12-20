using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly Action _onClose;

    [ObservableProperty] private bool _confirmBeforeDelete = true;
    [ObservableProperty] private bool _confirmBeforePurge = true;
    [ObservableProperty] private bool _autoRefreshMessages;
    [ObservableProperty] private int _autoRefreshIntervalSeconds = 30;
    [ObservableProperty] private int _defaultMessageCount = 100;
    [ObservableProperty] private bool _showDeadLetterBadges = true;
    [ObservableProperty] private bool _enableMessagePreview = true;
    [ObservableProperty] private string _theme = "Light";

    public string[] AvailableThemes { get; } = ["Light", "Dark", "System"];
    public int[] AvailableMessageCounts { get; } = [25, 50, 100, 200, 500];
    public int[] AvailableRefreshIntervals { get; } = [10, 30, 60, 120, 300];

    public SettingsViewModel(Action onClose)
    {
        _onClose = onClose;
        LoadSettings();
    }

    partial void OnThemeChanged(string value)
    {
        // Apply theme immediately when changed
        App.Instance?.ApplyTheme(value);
    }

    private void LoadSettings()
    {
        // Load settings from preferences/storage
        // For now, use defaults - these could be persisted to a JSON file
        ConfirmBeforeDelete = Preferences.ConfirmBeforeDelete;
        ConfirmBeforePurge = Preferences.ConfirmBeforePurge;
        AutoRefreshMessages = Preferences.AutoRefreshMessages;
        AutoRefreshIntervalSeconds = Preferences.AutoRefreshIntervalSeconds;
        DefaultMessageCount = Preferences.DefaultMessageCount;
        ShowDeadLetterBadges = Preferences.ShowDeadLetterBadges;
        EnableMessagePreview = Preferences.EnableMessagePreview;
        Theme = Preferences.Theme;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        // Save settings to preferences/storage
        Preferences.ConfirmBeforeDelete = ConfirmBeforeDelete;
        Preferences.ConfirmBeforePurge = ConfirmBeforePurge;
        Preferences.AutoRefreshMessages = AutoRefreshMessages;
        Preferences.AutoRefreshIntervalSeconds = AutoRefreshIntervalSeconds;
        Preferences.DefaultMessageCount = DefaultMessageCount;
        Preferences.ShowDeadLetterBadges = ShowDeadLetterBadges;
        Preferences.EnableMessagePreview = EnableMessagePreview;
        Preferences.Theme = Theme;
        Preferences.Save();
        
        _onClose();
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        ConfirmBeforeDelete = true;
        ConfirmBeforePurge = true;
        AutoRefreshMessages = false;
        AutoRefreshIntervalSeconds = 30;
        DefaultMessageCount = 100;
        ShowDeadLetterBadges = true;
        EnableMessagePreview = true;
        Theme = "Light";
    }
}

/// <summary>
/// Simple static class to hold user preferences.
/// In a real application, this would persist to a file or other storage.
/// </summary>
public static class Preferences
{
    private static readonly string PreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BusLane",
        "preferences.json"
    );

    public static bool ConfirmBeforeDelete { get; set; } = true;
    public static bool ConfirmBeforePurge { get; set; } = true;
    public static bool AutoRefreshMessages { get; set; }
    public static int AutoRefreshIntervalSeconds { get; set; } = 30;
    public static int DefaultMessageCount { get; set; } = 100;
    public static bool ShowDeadLetterBadges { get; set; } = true;
    public static bool EnableMessagePreview { get; set; } = true;
    public static string Theme { get; set; } = "Light";

    static Preferences()
    {
        Load();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(PreferencesPath))
            {
                var json = File.ReadAllText(PreferencesPath);
                var data = System.Text.Json.JsonSerializer.Deserialize<PreferencesData>(json);
                if (data != null)
                {
                    ConfirmBeforeDelete = data.ConfirmBeforeDelete;
                    ConfirmBeforePurge = data.ConfirmBeforePurge;
                    AutoRefreshMessages = data.AutoRefreshMessages;
                    AutoRefreshIntervalSeconds = data.AutoRefreshIntervalSeconds;
                    DefaultMessageCount = data.DefaultMessageCount;
                    ShowDeadLetterBadges = data.ShowDeadLetterBadges;
                    EnableMessagePreview = data.EnableMessagePreview;
                    Theme = data.Theme ?? "Light";
                }
            }
        }
        catch
        {
            // Use defaults if loading fails
        }
    }

    public static void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencesPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
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
                ShowDeadLetterBadges = ShowDeadLetterBadges,
                EnableMessagePreview = EnableMessagePreview,
                Theme = Theme
            };

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(PreferencesPath, json);
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
        public string? Theme { get; set; }
    }
}

