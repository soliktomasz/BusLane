namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Models.Logging;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Services.Abstractions;
using Services.Auth;
using Services.Infrastructure;
using Services.Monitoring;
using Services.ServiceBus;
using Services.Storage;
using Services.Diagnostics;
using Services.Security;
using Services.Terminal;
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
    private readonly IConnectionBackupService _connectionBackupService;
    private readonly IKeyboardShortcutService _keyboardShortcutService;
    private readonly IUpdateService _updateService;
    private readonly IDiagnosticBundleService _diagnosticBundleService;
    private readonly IAppLockService _appLockService;
    private readonly IBiometricAuthService _biometricAuthService;
    private readonly ILogSink _logSink;
    private IFileDialogService? _fileDialogService;
    private readonly IScheduledMessageStore? _scheduledMessageStore;
    private readonly SemaphoreSlim _startupInitializationGate = new(1, 1);
    private bool _startupInitialized;

    // Current operations instance - unified interface for all Service Bus operations
    private IServiceBusOperations? _operations;

    // Composed components
    public NavigationState Navigation { get; }
    public MessageOperationsViewModel MessageOps { get; }
    public SessionInspectorViewModel SessionInspector { get; }
    public ConnectionViewModel Connection { get; }
    public FeaturePanelsViewModel FeaturePanels { get; }
    public LogViewerViewModel LogViewer { get; }
    public TerminalHostViewModel Terminal { get; }
    public NamespaceSelectionViewModel NamespaceSelection { get; }
    public UpdateNotificationViewModel UpdateNotification { get; }
    public CommandPaletteViewModel CommandPalette { get; } = new();
    public IRelayCommand ShowIntroductionSplashCommand { get; }

    // Refactored components
    public TabManagementViewModel Tabs { get; }
    public MessageBulkOperationsViewModel BulkOps { get; }
    public ExportOperationsViewModel ExportOps { get; }
    public ConfirmationDialogViewModel Confirmation { get; }
    public EntityOperationsViewModel EntityOperations { get; }
    public AppLockViewModel AppLock { get; }
    public NamespaceTopologyOperationsViewModel TopologyOperations { get; }

    // Dashboard components
    public ViewModels.Dashboard.NamespaceDashboardViewModel NamespaceDashboard { get; }

    // Tab management (delegated to Tabs component)
    public ObservableCollection<ConnectionTabViewModel> ConnectionTabs => Tabs.ConnectionTabs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveTabs))]
    [NotifyPropertyChangedFor(nameof(ShellStatusMessage))]
    [NotifyPropertyChangedFor(nameof(ShellStatusSummary))]
    [NotifyPropertyChangedFor(nameof(CurrentNavigation))]
    [NotifyPropertyChangedFor(nameof(CurrentMessageOps))]
    [NotifyPropertyChangedFor(nameof(CurrentSessionInspector))]
    [NotifyPropertyChangedFor(nameof(HasActiveConnectionTab))]
    [NotifyPropertyChangedFor(nameof(IsActiveTabAzureMode))]
    [NotifyPropertyChangedFor(nameof(IsActiveTabConnectionStringMode))]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private ConnectionTabViewModel? _activeTab;

    public bool HasActiveTabs => ConnectionTabs.Count > 0;
    public string? ShellStatusMessage => ActiveTab?.StatusMessage ?? StatusMessage;
    public string? ShellStatusSummary => TruncateStatus(ShellStatusMessage);

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
    /// Gets a compact label describing the active workspace mode.
    /// </summary>
    public string ActiveWorkspaceModeLabel => ActiveTab?.Mode switch
    {
        ConnectionMode.AzureAccount => "Azure workspace",
        ConnectionMode.ConnectionString when ActiveTab?.SavedConnection != null => $"{ActiveTab.SavedConnection.TypeDisplayName} connection",
        ConnectionMode.ConnectionString => "Saved connection",
        _ => "Workspace"
    };

    /// <summary>
    /// Gets whether the active tab's entity pane is currently visible.
    /// </summary>
    public bool IsCurrentEntityPaneVisible => ActiveTab?.IsEntityPaneVisible ?? true;

    /// <summary>
    /// Gets whether to show the welcome screen (no active connection tab and not signed in).
    /// </summary>
    public bool ShowWelcome => !Connection.IsAuthenticated && !HasActiveConnectionTab;

    /// <summary>
    /// Gets whether to show the namespace selection prompt for Azure users before a workspace is active.
    /// </summary>
    public bool ShowNamespaceSelectionPrompt =>
        Connection.ShowAzureSections &&
        !HasActiveConnectionTab;

    /// <summary>
    /// Gets the navigation state for the active tab, or the legacy navigation if no tab is active.
    /// </summary>
    public NavigationState CurrentNavigation => ActiveTab?.Navigation ?? Navigation;

    /// <summary>
    /// Gets the message operations for the active tab, or the legacy message ops if no tab is active.
    /// </summary>
    public MessageOperationsViewModel CurrentMessageOps => ActiveTab?.MessageOps ?? MessageOps;

    /// <summary>
    /// Gets the session inspector for the active tab, or the legacy session inspector if no tab is active.
    /// </summary>
    public SessionInspectorViewModel CurrentSessionInspector => ActiveTab?.SessionInspector ?? SessionInspector;

    // UI State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showIntroductionSplash;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShellStatusMessage))]
    [NotifyPropertyChangedFor(nameof(ShellStatusSummary))]
    private string? _statusMessage;
    [ObservableProperty] private bool _showStatusPopup;

    // Send message popup
    [ObservableProperty] private bool _showSendMessagePopup;
    [ObservableProperty] private SendMessageViewModel? _sendMessageViewModel;

    // Create subscription dialog
    [ObservableProperty] private bool _showCreateSubscriptionDialog;
    [ObservableProperty] private TopicInfo? _createSubscriptionTopic;
    [ObservableProperty] private string _newSubscriptionName = "";
    [ObservableProperty] private bool _newSubscriptionRequiresSession;
    [ObservableProperty] private bool _isCreatingSubscription;
    private ConnectionTabViewModel? _createSubscriptionTab;
    private IServiceBusOperations? _createSubscriptionOperations;
    private ConnectionTabViewModel? _deleteSubscriptionTab;
    private IServiceBusOperations? _deleteSubscriptionOperations;

    // Subscription details dialog
    [ObservableProperty] private bool _showSubscriptionDetailDialog;
    [ObservableProperty] private SubscriptionInfo? _subscriptionDetailItem;

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
    private int _autoRefreshTickInProgress;
    private int _suppressDeadLetterReload;

    // Settings-driven computed properties
    public bool ShowDeadLetterBadges => _preferencesService.ShowDeadLetterBadges;
    public bool ShowTopicActionButtons => _preferencesService.ShowTopicActionButtons;
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
        IConnectionBackupService connectionBackupService,
        IVersionService versionService,
        IPreferencesService preferencesService,
        ILiveStreamService liveStreamService,
        IAlertService alertService,
        INotificationService notificationService,
        IKeyboardShortcutService keyboardShortcutService,
        IUpdateService updateService,
        IDiagnosticBundleService diagnosticBundleService,
        ITerminalSessionService terminalSessionService,
        IAppLockService appLockService,
        IBiometricAuthService biometricAuthService,
        ILogSink logSink,
        ViewModels.Dashboard.DashboardViewModel dashboardViewModel,
        ViewModels.Dashboard.NamespaceDashboardViewModel namespaceDashboardViewModel,
        IScheduledMessageStore? scheduledMessageStore = null,
        INamespaceTopologyService? namespaceTopologyService = null,
        IFileDialogService? fileDialogService = null)
    {
        _auth = auth;
        _azureResources = azureResources;
        _operationsFactory = operationsFactory;
        _connectionStorage = connectionStorage;
        _connectionBackupService = connectionBackupService;
        _versionService = versionService;
        _alertService = alertService;
        _preferencesService = preferencesService;
        _showIntroductionSplash = !preferencesService.HasSeenIntroduction;
        ShowIntroductionSplashCommand = new RelayCommand(() => ShowIntroductionSplash = true);
        _keyboardShortcutService = keyboardShortcutService;
        _updateService = updateService;
        _diagnosticBundleService = diagnosticBundleService;
        _appLockService = appLockService;
        _biometricAuthService = biometricAuthService;
        _logSink = logSink;
        _fileDialogService = fileDialogService;
        _scheduledMessageStore = scheduledMessageStore;

        // Initialize dashboard components
        NamespaceDashboard = namespaceDashboardViewModel;
        NamespaceDashboard.Inbox.UpdateActions(OpenInboxMessages, OpenInboxDeadLetter, OpenInboxSessionInspector);

        // Initialize composed components
        Navigation = new NavigationState(preferencesService);
        LogViewer = new LogViewerViewModel(logSink);
        Terminal = new TerminalHostViewModel(terminalSessionService, _preferencesService, msg => StatusMessage = msg);

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
        Connection.PropertyChanged += OnConnectionPropertyChanged;

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

        SessionInspector = new SessionInspectorViewModel(
            () => _operations,
            MessageOps,
            _logSink,
            () => Navigation.CurrentEntityName,
            () => Navigation.CurrentSubscriptionName,
            () => Navigation.CurrentEntityRequiresSession,
            index => Navigation.SelectedMessageTabIndex = index,
            msg => StatusMessage = msg);

        FeaturePanels = new FeaturePanelsViewModel(
            liveStreamService, alertService, notificationService,
            dashboardViewModel,
            () => ActiveTab?.Operations ?? _operations,
            () => CurrentNavigation.Queues,
            () => CurrentNavigation.Topics,
            () => CurrentNavigation.TopicSubscriptions,
            () => CurrentNavigation.SelectedQueue,
            () => CurrentNavigation.SelectedSubscription,
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
            () => _fileDialogService,
            msg => StatusMessage = msg);

        Confirmation = new ConfirmationDialogViewModel();
        TopologyOperations = new NamespaceTopologyOperationsViewModel(
            () => ActiveTab?.Operations ?? _operations,
            () => _fileDialogService,
            namespaceTopologyService,
            Confirmation,
            message => StatusMessage = message,
            loading => IsLoading = loading,
            RefreshActiveTabAsync);
        EntityOperations = new EntityOperationsViewModel(
            () => ActiveTab?.Operations ?? _operations,
            () => CurrentNavigation,
            SetWorkspaceStatusMessage,
            Confirmation,
            OpenSendMessagePopup);
        AppLock = new AppLockViewModel(_appLockService, biometricAuthService, CompleteStartupAfterUnlockAsync);

        UpdateNotification = new UpdateNotificationViewModel(updateService);

        // Wire up property change handlers for cross-component dependencies
        Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Navigation.SelectedAzureSubscription))
                FireAndForget(LoadNamespacesAsync(Navigation.SelectedAzureSubscription?.Id), nameof(LoadNamespacesAsync));
            else if (e.PropertyName == nameof(Navigation.ShowDeadLetter))
                TriggerDeadLetterReloadIfNeeded(MessageOps);
            else if (e.PropertyName == nameof(Navigation.SelectedMessageTabIndex) && Navigation.IsSessionInspectorTabSelected)
                FireAndForget(SessionInspector.LoadSessionsAsync(), nameof(SessionInspectorViewModel.LoadSessionsAsync));

            if (e.PropertyName == nameof(Navigation.SelectedNamespace))
            {
                OnPropertyChanged(nameof(ShowNamespaceSelectionPrompt));
            }
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

        // Update dashboard with operations and namespace info
        var namespaceId = ActiveTab?.Namespace?.Id ?? ActiveTab?.SavedConnection?.Name ?? "current-namespace";
        NamespaceDashboard.SetOperations(operations, namespaceId);
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
        SessionInspector.Clear();
        FeaturePanels.CloseAll();
        return Task.CompletedTask;
    }

    private void SetWorkspaceStatusMessage(string message)
    {
        if (ActiveTab != null)
        {
            ActiveTab.StatusMessage = message;
        }
        else
        {
            StatusMessage = message;
        }
    }

    private void InitializeAutoRefreshTimer()
    {
        _autoRefreshTimer = new System.Timers.Timer();
        _autoRefreshTimer.Elapsed += (_, _) => FireAndForget(HandleAutoRefreshTickAsync(), nameof(HandleAutoRefreshTickAsync));
        UpdateAutoRefreshTimer();
    }

    private async Task HandleAutoRefreshTickAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _autoRefreshTickInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (_disposed)
            {
                return;
            }

            if (_preferencesService.AutoRefreshMessages &&
                CurrentNavigation.CurrentEntityName != null &&
                CurrentMessageOps.Pagination.CurrentPage == 1 &&
                !CurrentMessageOps.IsLoadingMessages)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await CurrentMessageOps.LoadMessagesAsync();
                });
            }

            if (CurrentNavigation.Queues.Count > 0 || CurrentNavigation.TopicSubscriptions.Count > 0)
            {
                await _alertService.EvaluateAlertsAsync(CurrentNavigation.Queues, CurrentNavigation.TopicSubscriptions);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _autoRefreshTickInProgress, 0);
        }
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
        OnPropertyChanged(nameof(ShowTopicActionButtons));
        OnPropertyChanged(nameof(EnableMessagePreview));
        UpdateAutoRefreshTimer();
    }

    [RelayCommand]
    private void DismissIntroductionSplash()
    {
        _preferencesService.HasSeenIntroduction = true;
        _preferencesService.Save();
        ShowIntroductionSplash = false;
    }

    #region Initialization & Subscriptions

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await AppLock.InitializeAsync();
            if (AppLock.IsLocked)
            {
                StatusMessage = "Unlock BusLane to continue";
                return;
            }

            await EnsureStartupInitializedAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CompleteStartupAfterUnlockAsync()
    {
        IsLoading = true;
        try
        {
            await EnsureStartupInitializedAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to complete startup after unlock");
            StatusMessage = $"Unable to finish startup after unlock: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EnsureStartupInitializedAsync()
    {
        if (_startupInitialized)
        {
            return;
        }

        await _startupInitializationGate.WaitAsync();
        try
        {
            if (_startupInitialized)
            {
                return;
            }

            await Connection.InitializeAsync();
            await Tabs.RestoreTabSessionAsync();
            ScheduleStartupUpdateCheck();
            _startupInitialized = true;
        }
        finally
        {
            _startupInitializationGate.Release();
        }
    }

    private void ScheduleStartupUpdateCheck()
    {
        if (!_preferencesService.AutoCheckForUpdates)
        {
            return;
        }

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
        Navigation.SetPinScope(ns.Id);

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
        CurrentMessageOps.ClearSessionScope();
        CurrentSessionInspector.Clear();

        if (CurrentNavigation.IsSessionInspectorTabSelected)
        {
            await CurrentSessionInspector.LoadSessionsAsync();
            return;
        }

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
        CurrentSessionInspector.Clear();
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
        await SelectSubscriptionAsync(sub, CurrentNavigation, CurrentMessageOps, CurrentSessionInspector);
    }

    private async Task SelectSubscriptionAsync(
        SubscriptionInfo sub,
        NavigationState navigation,
        MessageOperationsViewModel messageOps,
        SessionInspectorViewModel sessionInspector)
    {
        navigation.SelectedSubscription = sub;
        navigation.SelectedQueue = null;
        navigation.SelectedTopic = null;
        navigation.SelectedEntity = sub;
        messageOps.ClearSessionScope();
        sessionInspector.Clear();

        if (navigation.IsSessionInspectorTabSelected)
        {
            await sessionInspector.LoadSessionsAsync();
            return;
        }

        await messageOps.LoadMessagesAsync();
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
            await ReloadTopicSubscriptionsAsync(topic, operations, CurrentNavigation);
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

    [RelayCommand]
    private void OpenCreateSubscriptionDialog(TopicInfo topic)
    {
        CreateSubscriptionTopic = topic;
        _createSubscriptionTab = ActiveTab;
        _createSubscriptionOperations = ActiveTab?.Operations ?? _operations;
        NewSubscriptionName = string.Empty;
        NewSubscriptionRequiresSession = false;
        ShowCreateSubscriptionDialog = true;
    }

    [RelayCommand]
    private void CloseCreateSubscriptionDialog()
    {
        ShowCreateSubscriptionDialog = false;
        CreateSubscriptionTopic = null;
        _createSubscriptionTab = null;
        _createSubscriptionOperations = null;
        NewSubscriptionName = string.Empty;
        NewSubscriptionRequiresSession = false;
    }

    [RelayCommand]
    private async Task CreateSubscriptionAsync()
    {
        var topic = CreateSubscriptionTopic;
        var operations = _createSubscriptionOperations;
        var navigation = _createSubscriptionTab?.Navigation ?? Navigation;
        var messageOps = _createSubscriptionTab?.MessageOps ?? MessageOps;
        var sessionInspector = _createSubscriptionTab?.SessionInspector ?? SessionInspector;

        if (topic == null || operations == null)
        {
            StatusMessage = "Select a topic before creating a subscription";
            return;
        }

        var subscriptionName = NewSubscriptionName.Trim();
        if (string.IsNullOrWhiteSpace(subscriptionName))
        {
            StatusMessage = "Subscription name is required";
            return;
        }

        IsCreatingSubscription = true;
        StatusMessage = $"Creating subscription '{subscriptionName}'...";

        try
        {
            var options = new SubscriptionCreationOptions(subscriptionName, NewSubscriptionRequiresSession);
            await operations.CreateSubscriptionAsync(topic.Name, options);
            await ReloadTopicSubscriptionsAsync(topic, operations, navigation);

            var createdSubscription = topic.Subscriptions.FirstOrDefault(subscription =>
                string.Equals(subscription.Name, subscriptionName, StringComparison.OrdinalIgnoreCase));

            if (createdSubscription != null)
            {
                topic.IsExpanded = true;
                navigation.SelectedTopic = topic;
                navigation.TopicSubscriptions.Clear();
                foreach (var subscription in topic.Subscriptions)
                    navigation.TopicSubscriptions.Add(subscription);
                await SelectSubscriptionAsync(createdSubscription, navigation, messageOps, sessionInspector);
            }

            StatusMessage = $"Subscription '{subscriptionName}' created";
            CloseCreateSubscriptionDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to create subscription: {ex.Message}";
        }
        finally
        {
            IsCreatingSubscription = false;
        }
    }

    [RelayCommand]
    private void OpenSubscriptionDetailDialog(SubscriptionInfo subscription)
    {
        SubscriptionDetailItem = subscription;
        ShowSubscriptionDetailDialog = true;
    }

    [RelayCommand]
    private void CloseSubscriptionDetailDialog()
    {
        ShowSubscriptionDetailDialog = false;
        SubscriptionDetailItem = null;
    }

    [RelayCommand]
    private void DeleteSubscriptionRequest(SubscriptionInfo subscription)
    {
        _deleteSubscriptionTab = ActiveTab;
        _deleteSubscriptionOperations = ActiveTab?.Operations ?? _operations;
        Confirmation.ShowConfirmation(
            "Delete subscription",
            $"Are you sure you want to delete subscription '{subscription.Name}' from topic '{subscription.TopicName}'? This action cannot be undone.",
            "Delete",
            () => DeleteSubscriptionAsync(subscription));
    }

    private async Task DeleteSubscriptionAsync(SubscriptionInfo subscription)
    {
        var operations = _deleteSubscriptionOperations;
        var navigation = _deleteSubscriptionTab?.Navigation ?? Navigation;
        var messageOps = _deleteSubscriptionTab?.MessageOps ?? MessageOps;
        var sessionInspector = _deleteSubscriptionTab?.SessionInspector ?? SessionInspector;
        if (operations == null)
        {
            StatusMessage = "No active connection";
            return;
        }

        StatusMessage = $"Deleting subscription '{subscription.Name}'...";

        try
        {
            await operations.DeleteSubscriptionAsync(subscription.TopicName, subscription.Name);

            var topic = navigation.Topics.FirstOrDefault(t =>
                string.Equals(t.Name, subscription.TopicName, StringComparison.OrdinalIgnoreCase));

            if (topic != null)
            {
                await ReloadTopicSubscriptionsAsync(topic, operations, navigation);
            }

            if (navigation.SelectedSubscription == subscription)
            {
                navigation.SelectedSubscription = null;
                navigation.SelectedEntity = null;
                messageOps.Clear();
                sessionInspector.Clear();
            }

            StatusMessage = $"Subscription '{subscription.Name}' deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to delete subscription: {ex.Message}";
        }
        finally
        {
            _deleteSubscriptionTab = null;
            _deleteSubscriptionOperations = null;
        }
    }

    private async Task ReloadTopicSubscriptionsAsync(
        TopicInfo topic,
        IServiceBusOperations operations,
        NavigationState navigation)
    {
        var subs = (await operations.GetSubscriptionsAsync(topic.Name)).ToList();

        topic.Subscriptions.Clear();
        foreach (var sub in subs)
            topic.Subscriptions.Add(sub);
        topic.SubscriptionCount = subs.Count;
        topic.SubscriptionsLoaded = true;

        if (navigation.SelectedTopic == topic ||
            string.Equals(navigation.CurrentEntityName, topic.Name, StringComparison.OrdinalIgnoreCase))
        {
            navigation.TopicSubscriptions.Clear();
            foreach (var sub in subs)
                navigation.TopicSubscriptions.Add(sub);
        }
    }

    [RelayCommand]
    private void ToggleSelectedEntityPin()
    {
        CurrentNavigation.TogglePin(CurrentNavigation.SelectedEntity);
    }

    [RelayCommand]
    private void ToggleEntityPin(object? entity)
    {
        CurrentNavigation.TogglePin(entity);
    }

    [RelayCommand]
    private async Task SelectPinnedEntityAsync(PinnedEntity? pin)
    {
        if (pin == null)
        {
            return;
        }

        switch (pin.Type)
        {
            case PinnedEntityType.Queue:
                var queue = CurrentNavigation.Queues.FirstOrDefault(q => q.Name == pin.Name);
                if (queue != null)
                {
                    await SelectQueueAsync(queue);
                }
                break;
            case PinnedEntityType.Topic:
                var topic = CurrentNavigation.Topics.FirstOrDefault(t => t.Name == pin.Name);
                if (topic != null)
                {
                    await SelectTopicAsync(topic);
                }
                break;
            case PinnedEntityType.Subscription:
                await SelectPinnedSubscriptionAsync(pin);
                break;
        }
    }

    private async Task SelectPinnedSubscriptionAsync(PinnedEntity pin)
    {
        if (string.IsNullOrWhiteSpace(pin.TopicName))
        {
            return;
        }

        var topic = CurrentNavigation.Topics.FirstOrDefault(t => t.Name == pin.TopicName);
        if (topic == null)
        {
            return;
        }

        await SelectTopicAsync(topic);
        var subscription = CurrentNavigation.TopicSubscriptions.FirstOrDefault(sub =>
            sub.TopicName == pin.TopicName &&
            sub.Name == pin.Name);
        if (subscription != null)
        {
            await SelectSubscriptionAsync(subscription);
        }
    }

    private void OpenInboxMessages(NamespaceInboxItem item)
    {
        FireAndForget(OpenInboxEntityAsync(item, selectedTabIndex: 0), nameof(OpenInboxMessages));
    }

    private void OpenInboxDeadLetter(NamespaceInboxItem item)
    {
        FireAndForget(OpenInboxEntityAsync(item, selectedTabIndex: 1), nameof(OpenInboxDeadLetter));
    }

    private void OpenInboxSessionInspector(NamespaceInboxItem item)
    {
        FireAndForget(OpenInboxEntityAsync(item, selectedTabIndex: 2), nameof(OpenInboxSessionInspector));
    }

    private async Task OpenInboxEntityAsync(NamespaceInboxItem item, int selectedTabIndex)
    {
        switch (item.EntityType)
        {
            case EntityType.Queue:
            {
                var queue = CurrentNavigation.Queues.FirstOrDefault(q =>
                    string.Equals(q.Name, item.EntityName, StringComparison.OrdinalIgnoreCase));

                if (queue == null)
                {
                    StatusMessage = $"Queue not found: {item.EntityName}";
                    return;
                }

                CurrentNavigation.SelectedQueue = queue;
                CurrentNavigation.SelectedTopic = null;
                CurrentNavigation.SelectedSubscription = null;
                CurrentNavigation.SelectedEntity = queue;
                CurrentNavigation.TopicSubscriptions.Clear();
                break;
            }
            case EntityType.Subscription:
            {
                var subscriptionName = GetInboxSubscriptionName(item);
                if (string.IsNullOrWhiteSpace(item.TopicName) || string.IsNullOrWhiteSpace(subscriptionName))
                {
                    StatusMessage = $"Subscription not found: {item.EntityName}";
                    return;
                }

                var selectedTopic = CurrentNavigation.Topics.FirstOrDefault(topic =>
                    string.Equals(topic.Name, item.TopicName, StringComparison.OrdinalIgnoreCase));

                var subscription = new SubscriptionInfo(
                    subscriptionName,
                    item.TopicName,
                    MessageCount: item.ActiveMessageCount + item.DeadLetterCount,
                    ActiveMessageCount: item.ActiveMessageCount,
                    DeadLetterCount: item.DeadLetterCount,
                    AccessedAt: DateTimeOffset.UtcNow,
                    RequiresSession: item.RequiresSession);

                CurrentNavigation.SelectedTopic = selectedTopic;
                CurrentNavigation.SelectedQueue = null;
                CurrentNavigation.SelectedSubscription = subscription;
                CurrentNavigation.SelectedEntity = subscription;
                break;
            }
            default:
                StatusMessage = $"Inbox navigation does not support {item.EntityType}";
                return;
        }

        CurrentMessageOps.ClearSessionScope();
        CurrentSessionInspector.Clear();
        CurrentNavigation.SelectedMessageTabIndex = selectedTabIndex;

        if (selectedTabIndex == 2)
        {
            await CurrentSessionInspector.LoadSessionsAsync();
            return;
        }

        await CurrentMessageOps.LoadMessagesAsync();
    }

    private static string? GetInboxSubscriptionName(NamespaceInboxItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TopicName))
        {
            return null;
        }

        var prefix = $"{item.TopicName}/";
        return item.EntityName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? item.EntityName[prefix.Length..]
            : null;
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
    private async Task CopyMessageBodyAsync(MessageInfo? message = null) => await CurrentMessageOps.CopyMessageBodyAsync(message);

    #endregion

    #region Namespace Topology

    [RelayCommand]
    private Task ExportNamespaceTopologyAsync(CancellationToken ct = default) => TopologyOperations.ExportAsync(ct);

    [RelayCommand]
    private Task ImportNamespaceTopologyAsync(CancellationToken ct = default) => TopologyOperations.ImportAsync(ct);

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
            _fileDialogService,
            scheduledMessageStore: _scheduledMessageStore);

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
            _fileDialogService,
            scheduledMessageStore: _scheduledMessageStore);

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

        var confirmationMessage = await BulkOps.GetPurgeConfirmationMessageAsync();
        Confirmation.ShowConfirmation(
            "Confirm Purge",
            confirmationMessage,
            BulkOps.GetPurgeConfirmText(),
            async () =>
            {
                await BulkOps.ExecutePurgeAsync();
                await CurrentMessageOps.LoadMessagesAsync();
            });
    }

    [RelayCommand]
    private async Task BulkResendMessagesAsync()
    {
        if (!CurrentMessageOps.HasSelectedMessages) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        Confirmation.ShowConfirmation(
            "Confirm Bulk Resend",
            BulkOps.GetBulkResendConfirmationMessage(CurrentMessageOps.SelectedMessages),
            BulkOps.GetBulkResendConfirmText(),
            async () =>
            {
                await BulkOps.ExecuteBulkResendAsync(CurrentMessageOps.SelectedMessages);
                CurrentMessageOps.SelectedMessages.Clear();
                await CurrentMessageOps.LoadMessagesAsync();
            });
    }

    [RelayCommand]
    private void BulkDeleteMessagesAsync()
    {
        if (!CurrentMessageOps.HasSelectedMessages) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        Confirmation.ShowConfirmation(
            "Confirm Bulk Delete",
            BulkOps.GetBulkDeleteConfirmationMessage(CurrentMessageOps.SelectedMessages),
            BulkOps.GetBulkDeleteConfirmText(),
            async () =>
            {
                var deletedCount = await BulkOps.ExecuteBulkDeleteAsync(CurrentMessageOps.SelectedMessages);
                CurrentMessageOps.SelectedMessages.Clear();
                if (deletedCount > 0)
                {
                    await RefreshCurrentEntityMetadataAsync();
                }
                await CurrentMessageOps.LoadMessagesAsync();
            });
    }

    private async Task RefreshCurrentEntityMetadataAsync()
    {
        if (CurrentNavigation.SelectedQueue is { } queue)
        {
            await EntityOperations.RefreshQueueCommand.ExecuteAsync(queue);
            return;
        }

        if (CurrentNavigation.SelectedSubscription is { } subscription)
        {
            await EntityOperations.RefreshSubscriptionCommand.ExecuteAsync(subscription);
        }
    }

    [RelayCommand]
    private void ResubmitDeadLetterMessagesAsync()
    {
        if (!CurrentMessageOps.HasSelectedMessages || !CurrentNavigation.ShowDeadLetter) return;

        var entityName = CurrentNavigation.CurrentEntityName;
        if (entityName == null) return;

        Confirmation.ShowConfirmation(
            "Confirm Resubmit Dead Letters",
            BulkOps.GetResubmitDeadLettersConfirmationMessage(CurrentMessageOps.SelectedMessages),
            "Resubmit",
            async () =>
            {
                await BulkOps.ExecuteResubmitDeadLettersAsync(CurrentMessageOps.SelectedMessages);
                CurrentMessageOps.SelectedMessages.Clear();
                await CurrentMessageOps.LoadMessagesAsync();
            });
    }

    [RelayCommand]
    private async Task ExportSelectedMessagesAsync()
    {
        await ExportOps.ExportSelectedMessagesAsync(CurrentMessageOps.SelectedMessages.ToList(), bodyOnly: false);
    }

    [RelayCommand]
    private async Task ExportSelectedMessageBodiesAsync()
    {
        await ExportOps.ExportSelectedMessagesAsync(CurrentMessageOps.SelectedMessages.ToList(), bodyOnly: true);
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

    private async Task RefreshActiveTabAsync()
    {
        var tab = ActiveTab;
        if (tab == null) return;

        IsLoading = true;
        try
        {
            await tab.RefreshAsync();
            StatusMessage = tab.StatusMessage;
        }
        catch (Exception ex)
        {
            tab.StatusMessage = ex.Message;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
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
    private void OpenCommandPalette()
    {
        CommandPalette.Open(BuildCommandPaletteItems());
    }

    [RelayCommand]
    private void CloseCommandPalette() => CommandPalette.Close();

    [RelayCommand]
    private async Task ExecuteCommandPaletteItemAsync(CommandPaletteItem? item)
    {
        if (item == null)
        {
            return;
        }

        CommandPalette.Close();
        try
        {
            await item.ExecuteAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Command failed: {ex.Message}";
        }
    }

    private IEnumerable<CommandPaletteItem> BuildCommandPaletteItems()
    {
        yield return new CommandPaletteItem(
            "Open My Connections",
            "Browse, add, import, or connect to saved Service Bus connections",
            "Connections",
            "Library",
            OpenConnectionLibraryAsync);

        if (!Connection.IsAuthenticated)
        {
            yield return new CommandPaletteItem(
                "Sign in with Azure",
                "Authenticate with Azure Identity and browse namespaces",
                "Connections",
                "Cloud",
                LoginAsync);
        }

        if (Connection.ShowAzureSections)
        {
            yield return new CommandPaletteItem(
                "Select Namespace",
                "Choose Azure Service Bus namespace workspace",
                "Connections",
                "Server",
                Run(OpenNamespacePanel));
        }

        foreach (var connection in Connection.SavedConnections
                     .OrderByDescending(c => c.IsFavorite)
                     .ThenBy(c => c.Name))
        {
            yield return new CommandPaletteItem(
                $"Connect to {connection.Name}",
                connection.EntityName ?? connection.Endpoint ?? connection.TypeDisplayName,
                "Saved Connections",
                "Cable",
                () => ConnectToSavedConnectionAsync(connection));
        }

        if (HasActiveConnectionTab)
        {
            yield return new CommandPaletteItem(
                "Refresh Workspace",
                "Reload entities for active tab",
                "Workspace",
                "RefreshCw",
                RefreshAsync);

            yield return new CommandPaletteItem(
                IsCurrentEntityPaneVisible ? "Hide Entity Pane" : "Show Entity Pane",
                "Toggle queue/topic browser pane",
                "Workspace",
                IsCurrentEntityPaneVisible ? "PanelLeftClose" : "PanelLeftOpen",
                Run(IsCurrentEntityPaneVisible ? HideEntityPane : ShowEntityPane));

            yield return new CommandPaletteItem(
                "Disconnect Workspace",
                "Close active Service Bus workspace",
                "Workspace",
                "PlugZap",
                Run(DisconnectConnection));
        }

        if (CurrentNavigation.CurrentEntityName != null)
        {
            yield return new CommandPaletteItem(
                "Refresh Messages",
                "Reload messages for selected entity",
                "Messages",
                "RefreshCw",
                LoadMessagesAsync);

            yield return new CommandPaletteItem(
                "Send Message",
                "Compose and send message to selected queue or topic",
                "Messages",
                "Send",
                Run(OpenSendMessagePopup));

            yield return new CommandPaletteItem(
                CurrentNavigation.ShowDeadLetter ? "Show Active Messages" : "Show Dead Letters",
                "Toggle active and dead-letter message view",
                "Messages",
                "Inbox",
                ToggleDeadLetterViewAsync);
        }

        foreach (var pin in CurrentNavigation.PinnedEntities)
        {
            yield return new CommandPaletteItem(
                $"Open {pin.DisplayName}",
                pin.TypeLabel,
                "Pinned Entities",
                "Pin",
                () => SelectPinnedEntityAsync(pin));
        }

        foreach (var queue in CurrentNavigation.Queues.OrderBy(q => q.Name))
        {
            yield return new CommandPaletteItem(
                $"Open queue {queue.Name}",
                $"{queue.ActiveMessageCount} active, {queue.DeadLetterCount} dead letters",
                "Queues",
                "Inbox",
                () => SelectQueueAsync(queue));
        }

        foreach (var topic in CurrentNavigation.Topics.OrderBy(t => t.Name))
        {
            yield return new CommandPaletteItem(
                $"Open topic {topic.Name}",
                $"{topic.SubscriptionCount} subscriptions",
                "Topics",
                "Send",
                () => SelectTopicAsync(topic));
        }

        foreach (var subscription in CurrentNavigation.TopicSubscriptions.OrderBy(s => s.TopicName).ThenBy(s => s.Name))
        {
            yield return new CommandPaletteItem(
                $"Open subscription {subscription.TopicName}/{subscription.Name}",
                $"{subscription.ActiveMessageCount} active, {subscription.DeadLetterCount} dead letters",
                "Subscriptions",
                "BookOpen",
                () => SelectSubscriptionAsync(subscription));
        }

        yield return new CommandPaletteItem(
            "Open Dashboard",
            "Namespace metrics, inbox, and entity summaries",
            "Features",
            "LayoutDashboard",
            Run(OpenCharts));

        yield return new CommandPaletteItem(
            "Open Live Stream",
            "Real-time message monitoring",
            "Features",
            "Radio",
            OpenLiveStream);

        yield return new CommandPaletteItem(
            "Open Alerts",
            "Manage alert rules and notifications",
            "Features",
            "Bell",
            Run(OpenAlerts));

        yield return new CommandPaletteItem(
            Terminal.ShowTerminalPanel ? "Hide Terminal" : "Show Terminal",
            "Toggle embedded terminal panel",
            "Tools",
            "Terminal",
            Run(ToggleTerminal));

        yield return new CommandPaletteItem(
            LogViewer.IsOpen ? "Hide Activity Log" : "Show Activity Log",
            "Inspect recent application logs",
            "Tools",
            "ScrollText",
            Run(ToggleLogViewer));

        yield return new CommandPaletteItem(
            "Open Settings",
            "Preferences, security, updates, and diagnostics",
            "General",
            "Settings",
            OpenSettingsAsync);

        yield return new CommandPaletteItem(
            "Show Keyboard Shortcuts",
            "View available keyboard shortcuts",
            "General",
            "Keyboard",
            Run(ShowKeyboardShortcutsHelp));
    }

    private static Func<Task> Run(Action action)
    {
        return () =>
        {
            action();
            return Task.CompletedTask;
        };
    }

    [RelayCommand]
    private void OpenNamespacePanel() => NamespaceSelection.Open();

    [RelayCommand]
    private void CloseNamespacePanel() => NamespaceSelection.Close();

    [RelayCommand]
    private void CloseStatusPopup() => ShowStatusPopup = false;

    private static string? TruncateStatus(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var firstLine = message.AsSpan();
        var newlineIndex = firstLine.IndexOfAny('\r', '\n');
        if (newlineIndex >= 0)
            firstLine = firstLine[..newlineIndex];

        var colonIndex = firstLine.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 40)
            return firstLine[..colonIndex].ToString();

        return firstLine.Length > 60
            ? string.Concat(firstLine[..57], "...")
            : firstLine.ToString();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var settingsViewModel = new SettingsViewModel(
            CloseSettings,
            _preferencesService,
            _appLockService,
            _biometricAuthService,
            snapshot => AppLock.ApplySettingsSnapshotAsync(snapshot),
            this,
            _updateService,
            _diagnosticBundleService);

        SettingsViewModel = settingsViewModel;
        ShowSettings = true;
        await settingsViewModel.InitializeAsync();
    }

    [RelayCommand]
    private void CloseSettings()
    {
        ShowSettings = false;
        SettingsViewModel?.Dispose();
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
    private void ToggleTerminal() => Terminal.ToggleVisibilityCommand.Execute(null);

    [RelayCommand]
    private void DockTerminal() => Terminal.DockCommand.Execute(null);

    [RelayCommand]
    private void UndockTerminal() => Terminal.UndockCommand.Execute(null);

    [RelayCommand]
    private void ClearTerminal() => Terminal.ClearOutputCommand.Execute(null);

    [RelayCommand]
    private async Task RestartTerminalAsync() => await Terminal.RestartCommand.ExecuteAsync(null);

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
        Interlocked.Increment(ref _suppressDeadLetterReload);
        try
        {
            CurrentNavigation.ShowDeadLetter = !CurrentNavigation.ShowDeadLetter;
        }
        finally
        {
            Interlocked.Decrement(ref _suppressDeadLetterReload);
        }

        await ReloadMessagesForDeadLetterAsync(CurrentMessageOps);
    }

    #endregion

    #region Feature Panels (delegated)

    [RelayCommand]
    private async Task OpenLiveStream()
    {
        NamespaceDashboard.Deactivate();
        await FeaturePanels.OpenLiveStream();
    }

    [RelayCommand]
    private void CloseLiveStream() => FeaturePanels.CloseLiveStream();

    [RelayCommand]
    private void OpenCharts()
    {
        FeaturePanels.OpenCharts();
        NamespaceDashboard.Activate();
    }

    [RelayCommand]
    private void CloseCharts()
    {
        FeaturePanels.CloseCharts();
        NamespaceDashboard.Deactivate();
    }

    [RelayCommand]
    private void OpenAlerts()
    {
        NamespaceDashboard.Deactivate();
        FeaturePanels.OpenAlerts();
    }

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
            _connectionBackupService,
            _operationsFactory,
            _fileDialogService,
            _logSink,
            async conn =>
            {
                Connection.ShowConnectionLibrary = false;
                Connection.ConnectionLibraryViewModel = null;
                await Tabs.OpenTabForConnectionAsync(conn);
            },
            msg => StatusMessage = msg,
            Connection.RefreshFavoriteConnectionsAsync,
            Connection.LoadSavedConnectionsAsync
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
    private void DisconnectConnection()
    {
        Confirmation.ShowConfirmation(
            "Confirm Disconnect",
            "Are you sure you want to disconnect from this workspace?",
            "Disconnect",
            async () =>
            {
                await Tabs.CloseActiveTabAsync();
                await Connection.DisconnectConnectionAsync();
            });
    }

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

    [RelayCommand]
    public void HideEntityPane()
    {
        if (ActiveTab == null)
        {
            return;
        }

        ActiveTab.IsEntityPaneVisible = false;
        Tabs.SaveTabSession();
    }

    [RelayCommand]
    public void ShowEntityPane()
    {
        if (ActiveTab == null)
        {
            return;
        }

        ActiveTab.IsEntityPaneVisible = true;
        Tabs.SaveTabSession();
    }

    // Track the currently subscribed tab for property change notifications
    private ConnectionTabViewModel? _subscribedTab;

    partial void OnActiveTabChanged(ConnectionTabViewModel? oldValue, ConnectionTabViewModel? newValue)
    {
        foreach (var tab in ConnectionTabs)
        {
            tab.IsActive = tab == newValue;
        }

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
        OnPropertyChanged(nameof(ShellStatusSummary));

        // Notify computed properties that depend on active tab state
        NotifyActiveTabDependentProperties();

        // Keep fallback operations in sync with active tab operations.
        SetOperations(newValue?.Operations);

        if (CurrentNavigation.IsSessionInspectorTabSelected)
        {
            FireAndForget(CurrentSessionInspector.LoadSessionsAsync(), nameof(SessionInspectorViewModel.LoadSessionsAsync));
        }

    }

    private void OnActiveTabNavigationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationState.ShowDeadLetter))
        {
            TriggerDeadLetterReloadIfNeeded(CurrentMessageOps);
        }
        else if (e.PropertyName == nameof(NavigationState.SelectedMessageTabIndex) && CurrentNavigation.IsSessionInspectorTabSelected)
        {
            FireAndForget(CurrentSessionInspector.LoadSessionsAsync(), nameof(SessionInspectorViewModel.LoadSessionsAsync));
        }

        if (e.PropertyName == nameof(NavigationState.SelectedNamespace))
        {
            OnPropertyChanged(nameof(ShowNamespaceSelectionPrompt));
        }
    }

    private void OnActiveTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ConnectionTabViewModel.StatusMessage))
        {
            OnPropertyChanged(nameof(ShellStatusMessage));
            OnPropertyChanged(nameof(ShellStatusSummary));
        }

        // When the active tab's IsConnected or Mode changes, notify computed properties
        if (e.PropertyName is nameof(ConnectionTabViewModel.IsConnected) or nameof(ConnectionTabViewModel.Mode))
        {
            NotifyActiveTabDependentProperties();
        }
        else if (e.PropertyName == nameof(ConnectionTabViewModel.IsEntityPaneVisible))
        {
            OnPropertyChanged(nameof(IsCurrentEntityPaneVisible));
        }

        // Update dashboard operations when connection state changes
        if (e.PropertyName == nameof(ConnectionTabViewModel.IsConnected))
        {
            var tab = sender as ConnectionTabViewModel;
            if (tab?.IsConnected == true)
            {
                var namespaceId = tab.Namespace?.Id ?? tab.SavedConnection?.Name ?? "current-namespace";
                NamespaceDashboard.SetOperations(tab.Operations, namespaceId);
            }
        }

        // Also notify for SavedConnection and Namespace so bindings update properly
        if (e.PropertyName is nameof(ConnectionTabViewModel.SavedConnection) or nameof(ConnectionTabViewModel.Namespace))
        {
            OnPropertyChanged(nameof(ActiveTab));
            OnPropertyChanged(nameof(ActiveWorkspaceModeLabel));
        }
    }

    private void OnConnectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConnectionViewModel.IsAuthenticated)
            or nameof(ConnectionViewModel.CurrentMode)
            or nameof(ConnectionViewModel.ShowAzureSections))
        {
            OnPropertyChanged(nameof(ShowWelcome));
            OnPropertyChanged(nameof(ShowNamespaceSelectionPrompt));
        }
    }

    private void NotifyActiveTabDependentProperties()
    {
        OnPropertyChanged(nameof(HasActiveConnectionTab));
        OnPropertyChanged(nameof(IsActiveTabAzureMode));
        OnPropertyChanged(nameof(IsActiveTabConnectionStringMode));
        OnPropertyChanged(nameof(ActiveWorkspaceModeLabel));
        OnPropertyChanged(nameof(IsCurrentEntityPaneVisible));
        OnPropertyChanged(nameof(ShowWelcome));
        OnPropertyChanged(nameof(ShowNamespaceSelectionPrompt));
        OnPropertyChanged(nameof(CurrentNavigation));
        OnPropertyChanged(nameof(CurrentMessageOps));
        OnPropertyChanged(nameof(CurrentSessionInspector));
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

    private void TriggerDeadLetterReloadIfNeeded(MessageOperationsViewModel messageOperations)
    {
        if (Volatile.Read(ref _suppressDeadLetterReload) != 0)
        {
            return;
        }

        FireAndForget(ReloadMessagesForDeadLetterAsync(messageOperations), nameof(ReloadMessagesForDeadLetterAsync));
    }

    private static Task ReloadMessagesForDeadLetterAsync(MessageOperationsViewModel messageOperations)
    {
        return messageOperations.LoadMessagesAsync();
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
        Terminal?.Dispose();

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
        await Terminal.DisposeAsync();

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
