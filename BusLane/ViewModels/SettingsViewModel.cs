using Avalonia.Threading;
using BusLane.Services.Abstractions;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly Action _onClose;
    private readonly MainWindowViewModel? _mainViewModel;
    private readonly IPreferencesService _preferencesService;
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

    public SettingsViewModel(
        Action onClose,
        IPreferencesService preferencesService,
        MainWindowViewModel? mainViewModel = null)
    {
        _onClose = onClose;
        _preferencesService = preferencesService;
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

            ConfirmBeforeDelete = _preferencesService.ConfirmBeforeDelete;
            ConfirmBeforePurge = _preferencesService.ConfirmBeforePurge;
            AutoRefreshMessages = _preferencesService.AutoRefreshMessages;
            AutoRefreshIntervalSeconds = _preferencesService.AutoRefreshIntervalSeconds;
            DefaultMessageCount = _preferencesService.DefaultMessageCount;
            ShowDeadLetterBadges = _preferencesService.ShowDeadLetterBadges;
            EnableMessagePreview = _preferencesService.EnableMessagePreview;
            Theme = _preferencesService.Theme;
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

        _preferencesService.ConfirmBeforeDelete = ConfirmBeforeDelete;
        _preferencesService.ConfirmBeforePurge = ConfirmBeforePurge;
        _preferencesService.AutoRefreshMessages = AutoRefreshMessages;
        _preferencesService.AutoRefreshIntervalSeconds = AutoRefreshIntervalSeconds;
        _preferencesService.DefaultMessageCount = DefaultMessageCount;
        _preferencesService.ShowDeadLetterBadges = ShowDeadLetterBadges;
        _preferencesService.EnableMessagePreview = EnableMessagePreview;
        _preferencesService.Theme = themeToApply;
        _preferencesService.Save();
        
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


