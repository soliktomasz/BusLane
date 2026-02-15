namespace BusLane.ViewModels;

using Avalonia.Threading;
using BusLane.Services.Abstractions;
using BusLane.Services.Update;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly Action _onClose;
    private readonly MainWindowViewModel? _mainViewModel;
    private readonly IPreferencesService _preferencesService;
    private readonly IUpdateService? _updateService;
    private string _originalTheme = "Light";
    private bool _isLoading;

    [ObservableProperty] private bool _confirmBeforeDelete = true;
    [ObservableProperty] private bool _confirmBeforePurge = true;
    [ObservableProperty] private bool _autoRefreshMessages;
    [ObservableProperty] private int _autoRefreshIntervalSeconds = 30;
    [ObservableProperty] private int _defaultMessageCount = 100;
    [ObservableProperty] private int _messagesPerPage = 100;
    [ObservableProperty] private int _maxTotalMessages = 500;
    [ObservableProperty] private bool _showDeadLetterBadges = true;
    [ObservableProperty] private bool _enableMessagePreview = true;
    [ObservableProperty] private string _theme = "Light";
    [ObservableProperty] private bool _restoreTabsOnStartup = true;
    [ObservableProperty] private bool _enableTelemetry;
    [ObservableProperty] private bool _autoCheckForUpdates = true;
    [ObservableProperty] private bool _isCheckingForUpdates;

    public string[] AvailableThemes { get; } = ["Light", "Dark", "System"];
    public int[] AvailableMessageCounts { get; } = [25, 50, 100, 200, 500];
    public int[] MessagesPerPageOptions { get; } = [25, 50, 100];
    public int[] MaxTotalMessagesOptions { get; } = [100, 250, 500, 1000];
    public int[] AvailableRefreshIntervals { get; } = [10, 30, 60, 120, 300];

    public SettingsViewModel(
        Action onClose,
        IPreferencesService preferencesService,
        MainWindowViewModel? mainViewModel = null,
        IUpdateService? updateService = null)
    {
        _onClose = onClose;
        _preferencesService = preferencesService;
        _mainViewModel = mainViewModel;
        _updateService = updateService;

        // Capture original theme BEFORE loading to avoid any binding interference
        _originalTheme = preferencesService.Theme;

        LoadSettings();

        // Schedule the end of loading state to happen after UI bindings are established
        Dispatcher.UIThread.Post(() => _isLoading = false, DispatcherPriority.Loaded);
    }

    partial void OnThemeChanged(string value)
    {
        // Skip theme preview during initial load, binding initialization, or dialog closing
        // Also skip if value is null/empty (can happen during binding teardown)
        if (_isLoading || string.IsNullOrEmpty(value))
            return;

        // Apply theme immediately as preview when user changes it
        App.Instance?.ApplyTheme(value);
    }

    private void LoadSettings()
    {
        _isLoading = true;

        try
        {
            ConfirmBeforeDelete = _preferencesService.ConfirmBeforeDelete;
            ConfirmBeforePurge = _preferencesService.ConfirmBeforePurge;
            AutoRefreshMessages = _preferencesService.AutoRefreshMessages;
            AutoRefreshIntervalSeconds = _preferencesService.AutoRefreshIntervalSeconds;
            DefaultMessageCount = _preferencesService.DefaultMessageCount;
            MessagesPerPage = _preferencesService.MessagesPerPage;
            MaxTotalMessages = _preferencesService.MaxTotalMessages;
            ShowDeadLetterBadges = _preferencesService.ShowDeadLetterBadges;
            EnableMessagePreview = _preferencesService.EnableMessagePreview;
            Theme = _preferencesService.Theme;
            RestoreTabsOnStartup = _preferencesService.RestoreTabsOnStartup;
            EnableTelemetry = _preferencesService.EnableTelemetry;
            AutoCheckForUpdates = _preferencesService.AutoCheckForUpdates;
            // Note: _isLoading is set to false via Dispatcher in constructor (normal case)
        }
        catch
        {
            // If loading fails, reset _isLoading immediately to avoid stuck state
            _isLoading = false;
            throw;
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
        _preferencesService.MessagesPerPage = MessagesPerPage;
        _preferencesService.MaxTotalMessages = MaxTotalMessages;
        _preferencesService.ShowDeadLetterBadges = ShowDeadLetterBadges;
        _preferencesService.EnableMessagePreview = EnableMessagePreview;
        _preferencesService.Theme = themeToApply;
        _preferencesService.RestoreTabsOnStartup = RestoreTabsOnStartup;
        _preferencesService.EnableTelemetry = EnableTelemetry;
        _preferencesService.AutoCheckForUpdates = AutoCheckForUpdates;
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
        // Block any further theme changes from binding teardown
        _isLoading = true;

        var themeToRestore = _originalTheme;
        _onClose();

        // Apply theme AFTER dialog is fully closed to override any binding-triggered changes
        Dispatcher.UIThread.Post(() =>
        {
            App.Instance?.ApplyTheme(themeToRestore);
        }, DispatcherPriority.Background);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        ConfirmBeforeDelete = true;
        ConfirmBeforePurge = true;
        AutoRefreshMessages = false;
        AutoRefreshIntervalSeconds = 30;
        DefaultMessageCount = 100;
        MessagesPerPage = 100;
        MaxTotalMessages = 500;
        ShowDeadLetterBadges = true;
        EnableMessagePreview = true;
        Theme = "Light";
        RestoreTabsOnStartup = true;
        EnableTelemetry = false;
        AutoCheckForUpdates = true;
    }

    [RelayCommand]
    private async Task CheckForUpdatesNowAsync()
    {
        if (_updateService == null || IsCheckingForUpdates)
            return;

        try
        {
            IsCheckingForUpdates = true;
            await _updateService.CheckForUpdatesAsync(manualCheck: true);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
}

