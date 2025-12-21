using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly Action _onClose;
    private readonly MainWindowViewModel? _mainViewModel;
    private string _originalTheme = "Light";
    private bool _isLoading;

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

    public SettingsViewModel(Action onClose, MainWindowViewModel? mainViewModel = null)
    {
        _onClose = onClose;
        _mainViewModel = mainViewModel;
        LoadSettings();
        _originalTheme = Theme;
    }

    partial void OnThemeChanged(string value)
    {
        // Skip theme preview during initial load
        if (_isLoading)
            return;
            
        // Apply theme immediately as preview when user changes it
        App.Instance?.ApplyTheme(value);
    }

    private void LoadSettings()
    {
        try
        {
            _isLoading = true;
            
            // Load settings from preferences/storage
            ConfirmBeforeDelete = Preferences.ConfirmBeforeDelete;
            ConfirmBeforePurge = Preferences.ConfirmBeforePurge;
            AutoRefreshMessages = Preferences.AutoRefreshMessages;
            AutoRefreshIntervalSeconds = Preferences.AutoRefreshIntervalSeconds;
            DefaultMessageCount = Preferences.DefaultMessageCount;
            ShowDeadLetterBadges = Preferences.ShowDeadLetterBadges;
            EnableMessagePreview = Preferences.EnableMessagePreview;
            
            // Use the generated property instead of the field
            Theme = Preferences.Theme;
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        // Store the theme we want to keep
        var themeToApply = Theme;
        
        // Save settings to preferences/storage
        Preferences.ConfirmBeforeDelete = ConfirmBeforeDelete;
        Preferences.ConfirmBeforePurge = ConfirmBeforePurge;
        Preferences.AutoRefreshMessages = AutoRefreshMessages;
        Preferences.AutoRefreshIntervalSeconds = AutoRefreshIntervalSeconds;
        Preferences.DefaultMessageCount = DefaultMessageCount;
        Preferences.ShowDeadLetterBadges = ShowDeadLetterBadges;
        Preferences.EnableMessagePreview = EnableMessagePreview;
        Preferences.Theme = themeToApply;
        Preferences.Save();
        
        // Close the dialog
        _onClose();
        
        // Notify main view model about settings changes
        _mainViewModel?.NotifySettingsChanged();
        
        // Use Dispatcher to re-apply theme after UI has updated
        Dispatcher.UIThread.Post(() =>
        {
            App.Instance?.ApplyTheme(themeToApply);
        }, DispatcherPriority.Background);
    }

    [RelayCommand]
    private void Cancel()
    {
        // Revert theme to original if it was changed
        if (Theme != _originalTheme)
        {
            App.Instance?.ApplyTheme(_originalTheme);
        }
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

