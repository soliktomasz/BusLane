using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusLane.ViewModels;

using Services.Abstractions;
using Services.Auth;
using Services.Infrastructure;
using Services.Monitoring;
using Services.ServiceBus;
using Services.Storage;
using Services.Update;

/// <summary>
/// Represents a group of keyboard shortcuts for display in the help dialog.
/// </summary>
public record KeyboardShortcutGroup(string Category, IReadOnlyList<KeyboardShortcut> Shortcuts);

public enum ConnectionMode
{
    None,
    AzureAccount,
    ConnectionString
}

/// <summary>
/// Main window view model - slim coordinator that composes specialized components.
/// Responsibilities: coordination, UI state, and glue between components.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable, IAsyncDisposable
{
    private bool _disposed;

    // Services (injected)
    private readonly IAzureAuthService _auth;
    private readonly IAzureResourceService _azureResources;
    private readonly IServiceBusOperationsFactory _operationsFactory;
    private readonly IVersionService _versionService;
    private readonly IAlertService _alertService;
    private readonly IPreferencesService _preferencesService;
    private readonly IConnectionStorageService _connectionStorage;
    private readonly IKeyboardShortcutService _keyboardShortcutService;
    private readonly IUpdateService _updateService;
    private readonly ILogSink _logSink;
    private IFileDialogService? _fileDialogService;

    // Current operations instance - unified interface for all Service Bus operations
    private IServiceBusOperations? _operations;

    // Composed components
    public NavigationState Navigation { get; }
    public MessageOperationsViewModel MessageOps { get; }
    public ConnectionViewModel Connection { get; }
    public FeaturePanelsViewModel FeaturePanels { get; }
    public LogViewerViewModel LogViewer { get; }
    public NamespaceSelectionViewModel NamespaceSelection { get; }
    public UpdateNotificationViewModel UpdateNotification { get; }

    // Refactored components
    public TabManagementViewModel Tabs { get; }
    public MessageBulkOperationsViewModel BulkOps { get; }
    public ExportOperationsViewModel ExportOps { get; }
    public ConfirmationDialogViewModel Confirmation { get; }

    // Tab management (delegated to Tabs component)
    public ObservableCollection<ConnectionTabViewModel> ConnectionTabs => Tabs.ConnectionTabs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveTabs))]
    [NotifyPropertyChangedFor(nameof(ShellStatusMessage))]
    [NotifyPropertyChangedFor(nameof(CurrentNavigation))]
    [NotifyPropertyChangedFor(nameof(CurrentMessageOps))]
    [NotifyPropertyChangedFor(nameof(HasActiveConnectionTab))]
    [NotifyPropertyChangedFor(nameof(IsActiveTabAzureMode))]
    [NotifyPropertyChangedFor(nameof(IsActiveTabConnectionStringMode))]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private ConnectionTabViewModel? _activeTab;

    public bool HasActiveTabs => ConnectionTabs.Count > 0;
    public string? ShellStatusMessage => ActiveTab?.StatusMessage ?? StatusMessage;

    /// <summary>
    /// Gets whether there's an active tab that is connected.
    /// </summary>
    public bool HasActiveConnectionTab => ActiveTab?.IsConnected == true;

    /// <summary>
    /// Gets whether the active tab is connected via Azure credentials (namespace mode).
    /// </summary>
    public bool IsActiveTabAzureMode => ActiveTab?.IsConnected == true && ActiveTab?.Mode == ConnectionMode.AzureAccount;

    /// <summary>
    /// Gets whether the active tab is connected via connection string.
    /// </summary>
    public bool IsActiveTabConnectionStringMode => ActiveTab?.IsConnected == true && ActiveTab?.Mode == ConnectionMode.ConnectionString;

    /// <summary>
    /// Gets whether to show the welcome screen (no active connection tab and not signed in).
    /// </summary>
    public bool ShowWelcome => !Connection.IsAuthenticated && !HasActiveConnectionTab;

    /// <summary>
    /// Gets the navigation state for the active tab, or the legacy navigation if no tab is active.
    /// </summary>
    public NavigationState CurrentNavigation => ActiveTab?.Navigation ?? Navigation;

    /// <summary>
    /// Gets the message operations for the active tab, or the legacy message ops if no tab is active.
    /// </summary>
    public MessageOperationsViewModel CurrentMessageOps => ActiveTab?.MessageOps ?? MessageOps;

    // UI State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _showStatusPopup;

    // Send message popup
    [ObservableProperty] private bool _showSendMessagePopup;
    [ObservableProperty] private SendMessageViewModel? _sendMessageViewModel;

    // Settings
    [ObservableProperty] private bool _showSettings;
    [ObservableProperty] private SettingsViewModel? _settingsViewModel;


    // Keyboard shortcuts dialog
    [ObservableProperty] private bool _showKeyboardShortcuts;

    // Device code authentication dialog
    [ObservableProperty] private bool _showDeviceCodeDialog;
    [ObservableProperty] private string _deviceCodeUserCode = "";
    [ObservableProperty] private string _deviceCodeUrl = "";
    [ObservableProperty] private string _deviceCodeMessage = "";

    // Auto-refresh
    private System.Timers.Timer? _autoRefreshTimer;

    // Settings-driven computed properties
    public bool ShowDeadLetterBadges => _preferencesService.ShowDeadLetterBadges;
    public bool EnableMessagePreview => _preferencesService.EnableMessagePreview;
    public bool IsNavigationPanelVisible => _preferencesService.ShowNavigationPanel;

    public string AppVersion => _versionService.DisplayVersion;

    /// <summary>Gets the keyboard shortcut service for handling shortcuts.</summary>
    public IKeyboardShortcutService KeyboardShortcuts => _keyboardShortcutService;

    /// <summary>Gets keyboard shortcuts grouped by category for display.</summary>
    public IReadOnlyList<KeyboardShortcutGroup> KeyboardShortcutGroups =>
        _keyboardShortcutService.GetAllShortcuts()
            .GroupBy(s => s.Category)
            .Select(g => new KeyboardShortcutGroup(g.Key, g.ToList()))
            .ToList();

    public MainWindowViewModel(
        IAzureAuthService auth,
        IAzureResourceService azureResources,
        IServiceBusOperationsFactory operationsFactory,
        IConnectionStorageService connectionStorage,
        IVersionService versionService,
        IPreferencesService preferencesService,
        ILiveStreamService liveStreamService,
        IAlertService alertService,
        INotificationService notificationService,
        IKeyboardShortcutService keyboardShortcutService,
        IUpdateService updateService,
        ILogSink logSink,
        ViewModels.Dashboard.DashboardViewModel dashboardViewModel,
        IFileDialogService? fileDialogService = null)
    {
        _auth = auth;
        _azureResources = azureResources;
        _operationsFactory = operationsFactory;
        _connectionStorage = connectionStorage;
        _versionService = versionService;
        _alertService = alertService;
        _preferencesService = preferencesService;
        _keyboardShortcutService = keyboardShortcutService;
        _updateService = updateService;
        _logSink = logSink;
        _fileDialogService = fileDialogService;

        // Initialize composed components
        Navigation = new NavigationState();
        LogViewer = new LogViewerViewModel(logSink);

        NamespaceSelection = new NamespaceSelectionViewModel(
            Navigation,
            SelectNamespaceAsync);

        Connection = new ConnectionViewModel(
            auth,
            connectionStorage,
            _logSink,
            msg => StatusMessage = msg,
            OnConnectedAsync,
            OnDisconnectedAsync,
            open => { if (open) NamespaceSelection.Open(); else NamespaceSelection.Close(); });

        MessageOps = new MessageOperationsViewModel(
            () => _operations,
            preferencesService,
            _logSink,
            () => Navigation.CurrentEntityName,
            () => Navigation.CurrentSubscriptionName,
            () => Navigation.CurrentEntityRequiresSession,
            () => Navigation.ShowDeadLetter,
            () => GetKnownMessageCount(),
            msg => StatusMessage = msg);

        FeaturePanels = new FeaturePanelsViewModel(
            liveStreamService, alertService, notificationService,
            dashboardViewModel,
            () => ActiveTab?.Operations ?? _operations,
            () => Navigation.Queues,
            () => Navigation.Topics,
            () => Navigation.TopicSubscriptions,
            () => Navigation.SelectedQueue,
            () => Navigation.SelectedSubscription,
            msg => StatusMessage = msg);

        // Initialize refactored components
        Tabs = new TabManagementViewModel(
            operationsFactory,
            preferencesService,
            connectionStorage,
            auth,
            _logSink,
            tab => ActiveTab = tab);

        BulkOps = new MessageBulkOperationsViewModel(
            () => ActiveTab?.Operations ?? _operations,
            () => CurrentNavigation,
            preferencesService,
            _logSink,
            msg => StatusMessage = msg);

        ExportOps = new ExportOperationsViewModel(
            () => Navigation,
            _fileDialogService,
            msg => StatusMessage = msg);

        Confirmation = new ConfirmationDialogViewModel();

        UpdateNotification = new UpdateNotificationViewModel(updateService);

        // Wire up property change handlers for cross-component dependencies
        Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Navigation.SelectedAzureSubscription))
                FireAndForget(LoadNamespacesAsync(Navigation.SelectedAzureSubscription?.Id), nameof(LoadNamespacesAsync));
            else if (e.PropertyName == nameof(Navigation.ShowDeadLetter))
                FireAndForget(MessageOps.LoadMessagesAsync(), "LoadMessagesAsync");
        };

        // Wire up device code authentication event
        _auth.DeviceCodeRequired += (_, info) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DeviceCodeUserCode = info.UserCode;
                DeviceCodeUrl = info.VerificationUri;
                DeviceCodeMessage = info.Message;
                ShowDeviceCodeDialog = true;
            });
        };

        // Close device code dialog when authentication completes
        _auth.AuthenticationChanged += (_, authenticated) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (authenticated)
                    ShowDeviceCodeDialog = false;
            });
        };

        InitializeAutoRefreshTimer();
    }

    public void SetFileDialogService(IFileDialogService fileDialogService) => _fileDialogService = fileDialogService;

    /// <summary>
    /// Sets the current operations instance and updates child ViewModels.
    /// </summary>
    private void SetOperations(IServiceBusOperations? operations)
    {
        _operations = operations;
    }

    /// <summary>
    /// Gets the known total message count for the currently selected entity.
    /// Returns the dead letter count if viewing DLQ, otherwise the active message count.
    /// </summary>
    private long GetKnownMessageCount()
    {
        if (Navigation.ShowDeadLetter)
        {
            return Navigation.SelectedQueue?.DeadLetterCount
                ?? Navigation.SelectedSubscription?.DeadLetterCount
                ?? 0;
        }

        return Navigation.SelectedQueue?.ActiveMessageCount
            ?? Navigation.SelectedSubscription?.ActiveMessageCount
            ?? 0;
    }

    private async Task OnConnectedAsync()
    {
        if (Connection.CurrentMode == ConnectionMode.AzureAccount)
        {
            await LoadSubscriptionsAsync();
        }
    }

    private Task OnDisconnectedAsync()
    {
        SetOperations(null);
        Navigation.Clear();
        MessageOps.Clear();
        FeaturePanels.CloseAll();
        return Task.CompletedTask;
    }

    private void InitializeAutoRefreshTimer()
    {
        _autoRefreshTimer = new System.Timers.Timer();
        _autoRefreshTimer.Elapsed += async (_, _) =>
        {
            if (_preferencesService.AutoRefreshMessages &&
                CurrentNavigation.CurrentEntityName != null &&
                CurrentMessageOps.Pagination.CurrentPage == 1 &&
                !CurrentMessageOps.IsLoadingMessages)
            {
                Console.WriteLine($"[AutoRefresh] Reloading messages for entity '{CurrentNavigation.CurrentEntityName}'");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await CurrentMessageOps.LoadMessagesAsync();
                });
            }

            if (CurrentNavigation.Queues.Count > 0 || CurrentNavigation.TopicSubscriptions.Count > 0)
            {
                await _alertService.EvaluateAlertsAsync(CurrentNavigation.Queues, CurrentNavigation.TopicSubscriptions);
            }
        };
        UpdateAutoRefreshTimer();
    }

    public void UpdateAutoRefreshTimer()
    {
        if (_autoRefreshTimer == null) return;

        if (_preferencesService.AutoRefreshMessages)
        {
            _autoRefreshTimer.Interval = _preferencesService.AutoRefreshIntervalSeconds * 1000;
            _autoRefreshTimer.Start();
        }
        else
        {
            _autoRefreshTimer.Stop();
        }
    }

    public void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(ShowDeadLetterBadges));
        OnPropertyChanged(nameof(EnableMessagePreview));
        UpdateAutoRefreshTimer();
    }

    #region Initialization & Subscriptions

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await Connection.InitializeAsync();
            await Tabs.RestoreTabSessionAsync();

            // Check for updates on startup (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await _updateService.CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Startup update check failed");
                }
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSubscriptionsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading Azure subscriptions...";

        Log.Debug("LoadSubscriptionsAsync called. Auth.IsAuthenticated={IsAuth}, Auth.ArmClient is {ArmClientStatus}",
            _auth.IsAuthenticated, _auth.ArmClient != null ? "initialized" : "NULL");

        try
        {
            Navigation.Subscriptions.Clear();
            foreach (var sub in await _azureResources.GetAzureSubscriptionsAsync())
                Navigation.Subscriptions.Add(sub);

            if (Navigation.Subscriptions.Count > 0)
            {
                Navigation.SelectedAzureSubscription = Navigation.Subscriptions[0];
                StatusMessage = $"Found {Navigation.Subscriptions.Count} subscription(s)";
            }
            else
            {
                StatusMessage = "No Azure subscriptions found";
                Log.Warning("No Azure subscriptions returned - check account permissions");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading subscriptions: {ex.Message}";
            Log.Error(ex, "Failed to load Azure subscriptions");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadNamespacesAsync(string? subscriptionId)
    {
        if (subscriptionId == null) return;

        IsLoading = true;
        StatusMessage = "Loading namespaces...";

        try
        {
            Navigation.Namespaces.Clear();
            foreach (var ns in await _azureResources.GetNamespacesAsync(subscriptionId))
                Navigation.Namespaces.Add(ns);
            StatusMessage = $"Found {Navigation.Namespaces.Count} namespace(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Entity Selection

    [RelayCommand]
    private async Task SelectNamespaceAsync(ServiceBusNamespace ns)
    {
        await Tabs.OpenTabForNamespaceAsync(ns);
        Navigation.SelectedNamespace = ns;

        if (ActiveTab != null)
        {
            try
            {
                await _alertService.EvaluateAlertsAsync(ActiveTab.Navigation.Queues, ActiveTab.Navigation.TopicSubscriptions);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Alert evaluation failed: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task SelectQueueAsync(QueueInfo queue)
    {
        CurrentNavigation.SelectedQueue = queue;
        CurrentNavigation.SelectedTopic = null;
        CurrentNavigation.SelectedSubscription = null;
        CurrentNavigation.SelectedEntity = queue;
        CurrentNavigation.TopicSubscriptions.Clear();
        await CurrentMessageOps.LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task SelectTopicAsync(TopicInfo topic)
    {
        // Use active tab's operations if available, otherwise fall back to main operations
        var operations = ActiveTab?.Operations ?? _operations;
        if (operations == null) return;

        CurrentNavigation.SelectedTopic = topic;
        CurrentNavigation.SelectedQueue = null;
        CurrentNavigation.SelectedSubscription = null;
        CurrentNavigation.SelectedEntity = topic;
        CurrentMessageOps.Clear();
        CurrentNavigation.TopicSubscriptions.Clear();

        IsLoading = true;
        StatusMessage = $"Loading subscriptions for {topic.Name}...";

        try
        {
            var subs = await operations.GetSubscriptionsAsync(topic.Name);
            foreach (var sub in subs)
                CurrentNavigation.TopicSubscriptions.Add(sub);

            StatusMessage = $"{CurrentNavigation.TopicSubscriptions.Count} subscription(s)";
            await _alertService.EvaluateAlertsAsync(CurrentNavigation.Queues, CurrentNavigation.TopicSubscriptions);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectSubscriptionAsync(SubscriptionInfo sub)
    {
        CurrentNavigation.SelectedSubscription = sub;
        CurrentNavigation.SelectedQueue = null;
        CurrentNavigation.SelectedEntity = sub;
        await CurrentMessageOps.LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task LoadTopicSubscriptionsAsync(TopicInfo topic)
    {
        // Use active tab's operations if available, otherwise fall back to main operations
        var operations = ActiveTab?.Operations ?? _operations;
        if (topic.SubscriptionsLoaded || topic.IsLoadingSubscriptions || operations == null) return;

        topic.IsLoadingSubscriptions = true;

        try
        {
            var subs = await operations.GetSubscriptionsAsync(topic.Name);

            topic.Subscriptions.Clear();
            foreach (var sub in subs)
                topic.Subscriptions.Add(sub);
            topic.SubscriptionsLoaded = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading subscriptions: {ex.Message}";
        }
        finally
        {
            topic.IsLoadingSubscriptions = false;
        }
    }

    #endregion

    #region Message Operations (delegated to MessageOps with some coordination)

    [RelayCommand]
    private async Task LoadMessagesAsync() => await CurrentMessageOps.LoadMessagesAsync();

    [RelayCommand]
    private void SelectMessage(MessageInfo message) => CurrentMessageOps.SelectMessage(message);

    [RelayCommand]
    private void ClearSelectedMessage() => CurrentMessageOps.ClearSelectedMessage();

    [RelayCommand]
    private void ToggleMultiSelectMode() => CurrentMessageOps.ToggleMultiSelectMode();

    [RelayCommand]
    private void ToggleMessageSelection(MessageInfo message) => CurrentMessageOps.ToggleMessageSelection(message);

    [RelayCommand]
    private void SelectAllMessages() => CurrentMessageOps.SelectAllMessages();

    [RelayCommand]
    private void DeselectAllMessages() => CurrentMessageOps.DeselectAllMessages();

    [RelayCommand]
    private void ToggleSortOrder() => CurrentMessageOps.ToggleSortOrder();

    [RelayCommand]
    private void ClearMessageSearch() => CurrentMessageOps.ClearMessageSearch();

    [RelayCommand]
    private async Task CopyMessageBodyAsync() => await CurrentMessageOps.CopyMessageBodyAsync();

    #endregion

    #region Send Message

    [RelayCommand]
    private void OpenSendMessagePopup()
    {
        var entityName = CurrentNavigation.CurrentEntityName;
        var operations = ActiveTab?.Operations ?? _operations;
        if (entityName == null || operations == null) return;

        SendMessageViewModel = new SendMessageViewModel(
            operations,
            entityName,
            CloseSendMessagePopup,
            msg => StatusMessage = msg,
            _fileDialogService);

        ShowSendMessagePopup = true;
    }

    private async void CloseSendMessagePopup()
    {
        ShowSendMessagePopup = false;
        SendMessageViewModel = null;
        await CurrentMessageOps.LoadMessagesAsync();
    }

    [RelayCommand]
    private void CancelSendMessage()
    {
        ShowSendMessagePopup = false;
        SendMessageViewModel = null;
    }

    [RelayCommand]
    private async Task ResendMessageAsync(MessageInfo? message = null)
    {
        var msg = message ?? CurrentMessageOps.SelectedMessage;
        var operations = ActiveTab?.Operations ?? _operations;
        if (msg == null || operations == null) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        IsLoading = true;
        StatusMessage = "Resending message...";

        try
        {
            var properties = msg.Properties.ToDictionary(p => p.Key, p => p.Value);

            await operations.SendMessageAsync(
                entityName, msg.Body, properties,
                msg.ContentType, msg.CorrelationId, null, msg.SessionId, msg.Subject,
                msg.To, msg.ReplyTo, msg.ReplyToSessionId, msg.PartitionKey, msg.TimeToLive, null);

            StatusMessage = "Message resent successfully";
            await CurrentMessageOps.LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resending message: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CloneMessage(MessageInfo? message = null)
    {
        var msg = message ?? CurrentMessageOps.SelectedMessage;
        var operations = ActiveTab?.Operations ?? _operations;
        if (msg == null || operations == null) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        SendMessageViewModel = new SendMessageViewModel(
            operations,
            entityName,
            CloseSendMessagePopup,
            status => StatusMessage = status,
            _fileDialogService);

        SendMessageViewModel.PopulateFromMessage(msg);
        ShowSendMessagePopup = true;
        CurrentMessageOps.ClearSelectedMessage();
    }

    #endregion

    #region Purge & Bulk Operations

    [RelayCommand]
    private async Task PurgeMessagesAsync()
    {
        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        if (await BulkOps.ShouldConfirmPurgeAsync())
        {
            Confirmation.ShowConfirmation(
                "Confirm Purge",
                BulkOps.GetPurgeConfirmationMessage(),
                "Purge",
                async () =>
                {
                    await BulkOps.ExecutePurgeAsync();
                    await CurrentMessageOps.LoadMessagesAsync();
                });
        }
        else
        {
            await BulkOps.ExecutePurgeAsync();
            await CurrentMessageOps.LoadMessagesAsync();
        }
    }

    [RelayCommand]
    private async Task BulkResendMessagesAsync()
    {
        if (!CurrentMessageOps.HasSelectedMessages) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        var count = CurrentMessageOps.SelectedMessagesCount;

        if (await BulkOps.ShouldConfirmBulkResendAsync())
        {
            Confirmation.ShowConfirmation(
                "Confirm Bulk Resend",
                BulkOps.GetBulkResendConfirmationMessage(count),
                "Resend",
                async () =>
                {
                    await BulkOps.ExecuteBulkResendAsync(CurrentMessageOps.SelectedMessages);
                    CurrentMessageOps.SelectedMessages.Clear();
                    await CurrentMessageOps.LoadMessagesAsync();
                });
        }
        else
        {
            await BulkOps.ExecuteBulkResendAsync(CurrentMessageOps.SelectedMessages);
            CurrentMessageOps.SelectedMessages.Clear();
            await CurrentMessageOps.LoadMessagesAsync();
        }
    }

    [RelayCommand]
    private void BulkDeleteMessagesAsync()
    {
        if (!CurrentMessageOps.HasSelectedMessages) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        var count = CurrentMessageOps.SelectedMessagesCount;

        Confirmation.ShowConfirmation(
            "Confirm Bulk Delete",
            BulkOps.GetBulkDeleteConfirmationMessage(count),
            "Delete",
            async () =>
            {
                await BulkOps.ExecuteBulkDeleteAsync(CurrentMessageOps.SelectedMessages);
                CurrentMessageOps.SelectedMessages.Clear();
                await CurrentMessageOps.LoadMessagesAsync();
            });
    }

    [RelayCommand]
    private void ResubmitDeadLetterMessagesAsync()
    {
        if (!CurrentMessageOps.HasSelectedMessages || !CurrentNavigation.ShowDeadLetter) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        var count = CurrentMessageOps.SelectedMessagesCount;

        Confirmation.ShowConfirmation(
            "Confirm Resubmit Dead Letters",
            BulkOps.GetResubmitDeadLettersConfirmationMessage(count),
            "Resubmit",
            async () =>
            {
                await BulkOps.ExecuteResubmitDeadLettersAsync(CurrentMessageOps.SelectedMessages);
                CurrentMessageOps.SelectedMessages.Clear();
                await CurrentMessageOps.LoadMessagesAsync();
            });
    }

    #endregion

    #region Refresh

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (ActiveTab != null)
        {
            await RefreshActiveTabAsync();
            return;
        }

        if (Navigation.SelectedNamespace != null)
        {
            await SelectNamespaceAsync(Navigation.SelectedNamespace);
        }
    }

    /// <summary>
    /// Refreshes the active tab's entity list.
    /// </summary>
    private async Task RefreshActiveTabAsync()
    {
        if (ActiveTab == null) return;

        IsLoading = true;
        StatusMessage = "Refreshing...";

        try
        {
            ActiveTab.Navigation.Clear();

            if (ActiveTab.Mode == ConnectionMode.ConnectionString && ActiveTab.SavedConnection != null)
            {
                await RefreshTabConnectionEntitiesAsync(ActiveTab);
            }
            else if (ActiveTab.Mode == ConnectionMode.AzureAccount && ActiveTab.Namespace != null)
            {
                await ActiveTab.RefreshNamespaceEntitiesAsync();
            }

            StatusMessage = "Refreshed successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes entities for a tab connected via connection string.
    /// </summary>
    private async Task RefreshTabConnectionEntitiesAsync(ConnectionTabViewModel tab)
    {
        if (tab.Operations == null || tab.SavedConnection == null) return;

        if (tab.SavedConnection.Type == ConnectionType.Namespace)
        {
            await tab.RefreshNamespaceEntitiesAsync();
        }
        else if (tab.SavedConnection.Type == ConnectionType.Queue && tab.SavedConnection.EntityName != null)
        {
            var queueInfo = await tab.Operations.GetQueueInfoAsync(tab.SavedConnection.EntityName);
            if (queueInfo != null)
            {
                tab.Navigation.Queues.Add(queueInfo);
                tab.Navigation.SelectedQueue = queueInfo;
                tab.Navigation.SelectedEntity = queueInfo;
            }
        }
        else if (tab.SavedConnection.Type == ConnectionType.Topic && tab.SavedConnection.EntityName != null)
        {
            var topicInfo = await tab.Operations.GetTopicInfoAsync(tab.SavedConnection.EntityName);
            if (topicInfo != null)
            {
                tab.Navigation.Topics.Add(topicInfo);
                tab.Navigation.SelectedTopic = topicInfo;
                tab.Navigation.SelectedEntity = topicInfo;

                var subs = await tab.Operations.GetSubscriptionsAsync(tab.SavedConnection.EntityName);
                foreach (var sub in subs)
                    tab.Navigation.TopicSubscriptions.Add(sub);
            }
        }
    }

    #endregion

    #region UI Commands

    [RelayCommand]
    private void ToggleStatusPopup() => ShowStatusPopup = !ShowStatusPopup;

    [RelayCommand]
    private void ToggleNavigationPanel()
    {
        _preferencesService.ShowNavigationPanel = !_preferencesService.ShowNavigationPanel;
        _preferencesService.Save();
        OnPropertyChanged(nameof(IsNavigationPanelVisible));
    }

    [RelayCommand]
    private void OpenNamespacePanel() => NamespaceSelection.Open();

    [RelayCommand]
    private void CloseNamespacePanel() => NamespaceSelection.Close();

    [RelayCommand]
    private void CloseStatusPopup() => ShowStatusPopup = false;

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsViewModel = new SettingsViewModel(CloseSettings, _preferencesService, this);
        ShowSettings = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        ShowSettings = false;
        SettingsViewModel = null;
    }

    [RelayCommand]
    private void ShowKeyboardShortcutsHelp() => ShowKeyboardShortcuts = true;

    [RelayCommand]
    private void CloseKeyboardShortcuts() => ShowKeyboardShortcuts = false;

    [RelayCommand]
    private void CloseDeviceCodeDialog() => ShowDeviceCodeDialog = false;

    [RelayCommand]
    private void ToggleLogViewer() => LogViewer.Toggle();

    [RelayCommand]
    private async Task CopyDeviceCodeAsync()
    {
        if (string.IsNullOrEmpty(DeviceCodeUserCode)) return;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(DeviceCodeUserCode);
                StatusMessage = "Code copied to clipboard";
            }
        }
    }

    [RelayCommand]
    private void OpenDeviceCodeUrl()
    {
        if (string.IsNullOrEmpty(DeviceCodeUrl)) return;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = DeviceCodeUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open browser: {ex.Message}";
        }
    }

    /// <summary>
    /// Toggles the dead letter view for the current entity.
    /// </summary>
    [RelayCommand]
    private async Task ToggleDeadLetterViewAsync()
    {
        CurrentNavigation.ShowDeadLetter = !CurrentNavigation.ShowDeadLetter;
        await CurrentMessageOps.LoadMessagesAsync();
    }

    #endregion

    #region Feature Panels (delegated)

    [RelayCommand]
    private async Task OpenLiveStream() => await FeaturePanels.OpenLiveStream();

    [RelayCommand]
    private void CloseLiveStream() => FeaturePanels.CloseLiveStream();

    [RelayCommand]
    private void OpenCharts() => FeaturePanels.OpenCharts();

    [RelayCommand]
    private void CloseCharts() => FeaturePanels.CloseCharts();

    [RelayCommand]
    private void OpenAlerts() => FeaturePanels.OpenAlerts();

    [RelayCommand]
    private void CloseAlerts() => FeaturePanels.CloseAlerts();

    [RelayCommand]
    private async Task StartLiveStreamForSelectedEntity() => await FeaturePanels.StartLiveStreamForSelectedEntity();

    [RelayCommand]
    private async Task EvaluateAlerts() => await FeaturePanels.EvaluateAlerts();

    #endregion

    #region Connection Commands (delegated)

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsLoading = true;
        try
        {
            await Connection.LoginAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync() => await Connection.LogoutAsync();

    [RelayCommand]
    private async Task OpenConnectionLibraryAsync()
    {
        Connection.ConnectionLibraryViewModel = new ConnectionLibraryViewModel(
            _connectionStorage,
            _operationsFactory,
            _logSink,
            async conn =>
            {
                Connection.ShowConnectionLibrary = false;
                Connection.ConnectionLibraryViewModel = null;
                await Tabs.OpenTabForConnectionAsync(conn);
            },
            msg => StatusMessage = msg,
            Connection.RefreshFavoriteConnectionsAsync
        );
        await Connection.ConnectionLibraryViewModel.LoadConnectionsAsync();
        Connection.ShowConnectionLibrary = true;
    }

    [RelayCommand]
    private void CloseConnectionLibrary() => Connection.CloseConnectionLibrary();

    [RelayCommand]
    private async Task ConnectToSavedConnectionAsync(SavedConnection connection)
    {
        Connection.CloseConnectionLibrary();
        await Tabs.OpenTabForConnectionAsync(connection);
    }

    [RelayCommand]
    private async Task DisconnectConnectionAsync() => await Connection.DisconnectConnectionAsync();

    [RelayCommand]
    private async Task RefreshConnectionAsync()
    {
        await RefreshAsync();
    }

    // Aliases for connection-string mode navigation
    [RelayCommand]
    private async Task SelectQueueForConnectionAsync(QueueInfo queue) => await SelectQueueAsync(queue);

    [RelayCommand]
    private async Task SelectTopicForConnectionAsync(TopicInfo topic) => await SelectTopicAsync(topic);

    [RelayCommand]
    private async Task SelectSubscriptionForConnectionAsync(SubscriptionInfo sub) => await SelectSubscriptionAsync(sub);

    #endregion

    [RelayCommand]
    private async Task ExportMessageAsync(MessageInfo? message = null)
    {
        var msg = message ?? MessageOps.SelectedMessage;
        if (msg == null)
        {
            StatusMessage = "No message selected";
            return;
        }

        await ExportOps.ExportMessageAsync(msg);
    }


    #region Tab Management

    /// <summary>
    /// Opens a new tab for the given saved connection.
    /// </summary>
    public async Task OpenTabForConnectionAsync(SavedConnection connection)
    {
        await Tabs.OpenTabForConnectionAsync(connection);
    }

    /// <summary>
    /// Opens a new tab for the given Azure namespace.
    /// </summary>
    public async Task OpenTabForNamespaceAsync(ServiceBusNamespace ns)
    {
        await Tabs.OpenTabForNamespaceAsync(ns);
    }

    /// <summary>
    /// Closes the specified tab.
    /// </summary>
    [RelayCommand]
    public async Task CloseTabAsync(string tabId)
    {
        await Tabs.CloseTabAsync(tabId);
    }

    /// <summary>
    /// Switches to the specified tab.
    /// </summary>
    public void SwitchToTab(string tabId)
    {
        Tabs.SwitchToTab(tabId);
    }

    /// <summary>
    /// Closes the currently active tab.
    /// </summary>
    [RelayCommand]
    public async Task CloseActiveTabAsync()
    {
        await Tabs.CloseActiveTabAsync();
    }

    // Track the currently subscribed tab for property change notifications
    private ConnectionTabViewModel? _subscribedTab;

    partial void OnActiveTabChanged(ConnectionTabViewModel? oldValue, ConnectionTabViewModel? newValue)
    {
        // Unsubscribe from old tab's property changes
        if (_subscribedTab != null)
        {
            _subscribedTab.PropertyChanged -= OnActiveTabPropertyChanged;
            _subscribedTab.Navigation.PropertyChanged -= OnActiveTabNavigationPropertyChanged;
            _subscribedTab = null;
        }

        // Subscribe to new tab's property changes
        if (newValue != null)
        {
            newValue.PropertyChanged += OnActiveTabPropertyChanged;
            newValue.Navigation.PropertyChanged += OnActiveTabNavigationPropertyChanged;
            _subscribedTab = newValue;
        }

        OnPropertyChanged(nameof(ShellStatusMessage));

        // Notify computed properties that depend on active tab state
        NotifyActiveTabDependentProperties();

        // Keep fallback operations in sync with active tab operations.
        SetOperations(newValue?.Operations);

    }

    private void OnActiveTabNavigationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationState.ShowDeadLetter))
        {
            FireAndForget(CurrentMessageOps.LoadMessagesAsync(), "LoadMessagesAsync");
        }
    }

    private void OnActiveTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When the active tab's IsConnected or Mode changes, notify computed properties
        if (e.PropertyName is nameof(ConnectionTabViewModel.IsConnected) or nameof(ConnectionTabViewModel.Mode))
        {
            NotifyActiveTabDependentProperties();
        }

        // Also notify for SavedConnection and Namespace so bindings update properly
        if (e.PropertyName is nameof(ConnectionTabViewModel.SavedConnection) or nameof(ConnectionTabViewModel.Namespace))
        {
            OnPropertyChanged(nameof(ActiveTab));
        }
    }

    private void NotifyActiveTabDependentProperties()
    {
        OnPropertyChanged(nameof(HasActiveConnectionTab));
        OnPropertyChanged(nameof(IsActiveTabAzureMode));
        OnPropertyChanged(nameof(IsActiveTabConnectionStringMode));
        OnPropertyChanged(nameof(ShowWelcome));
        OnPropertyChanged(nameof(CurrentNavigation));
        OnPropertyChanged(nameof(CurrentMessageOps));
    }

    /// <summary>
    /// Switches to the next tab in the list.
    /// </summary>
    [RelayCommand]
    public void NextTab()
    {
        Tabs.NextTab();
    }

    /// <summary>
    /// Switches to the previous tab in the list.
    /// </summary>
    [RelayCommand]
    public void PreviousTab()
    {
        Tabs.PreviousTab();
    }

    /// <summary>
    /// Switches to a tab by its 1-based index (for keyboard shortcuts).
    /// </summary>
    [RelayCommand]
    public void SwitchToTabByIndex(int index)
    {
        Tabs.SwitchToTabByIndex(index);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Safely executes an async task without awaiting, logging any exceptions.
    /// Use this for event handlers where fire-and-forget is necessary.
    /// </summary>
    private static async void FireAndForget(Task task, string operationName)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception in fire-and-forget operation: {OperationName}", operationName);
        }
    }

    #endregion

    #region IDisposable / IAsyncDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _autoRefreshTimer?.Stop();
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;

        // Dispose the log viewer to unsubscribe from events
        LogViewer?.Dispose();

        // Dispose update notification to unsubscribe from events
        UpdateNotification?.Dispose();

        // Dispose operations if they implement IDisposable (sync path only)
        if (_operations is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _autoRefreshTimer?.Stop();
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;

        LogViewer?.Dispose();

        // Dispose update notification to unsubscribe from events
        UpdateNotification?.Dispose();

        // Properly await async disposal of operations
        if (_operations is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_operations is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    #endregion
}
