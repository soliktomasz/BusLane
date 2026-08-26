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
    private static readonly TimeSpan NamespaceNavigationLoadWaitTimeout = TimeSpan.FromSeconds(1);
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
    private readonly IScheduledMessageManagementService? _scheduledMessageManagementService;
    private IServiceBusOperations? _scheduledCloneOperations;
    private readonly ICorrelationMessageCatalog _correlationMessageCatalog;
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
    [NotifyPropertyChangedFor(nameof(IsNamespaceOverviewVisible))]
    [NotifyPropertyChangedFor(nameof(IsAzureEntityWorkspaceVisible))]
    [NotifyPropertyChangedFor(nameof(IsConnectionStringEntityWorkspaceVisible))]
    [NotifyPropertyChangedFor(nameof(WorkspaceTopicName))]
    [NotifyPropertyChangedFor(nameof(WorkspaceEntityName))]
    [NotifyPropertyChangedFor(nameof(WorkspaceDestinationLabel))]
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

    /// <summary>Gets whether active namespace is displaying its Overview workspace.</summary>
    public bool IsNamespaceOverviewVisible =>
        ActiveTab?.IsConnected == true && ActiveTab.WorkspaceMode == NamespaceWorkspaceMode.Overview;

    /// <summary>Gets whether active Azure namespace is displaying entity content.</summary>
    public bool IsAzureEntityWorkspaceVisible =>
        IsActiveTabAzureMode && ActiveTab?.WorkspaceMode == NamespaceWorkspaceMode.Entity;

    /// <summary>Gets whether active connection-string namespace is displaying entity content.</summary>
    public bool IsConnectionStringEntityWorkspaceVisible =>
        IsActiveTabConnectionStringMode && ActiveTab?.WorkspaceMode == NamespaceWorkspaceMode.Entity;

    /// <summary>Gets optional topic segment for current entity breadcrumb.</summary>
    public string? WorkspaceTopicName
    {
        get
        {
            if (CurrentNavigation.SelectedSubscription is { } subscription)
            {
                return subscription.TopicName;
            }

            if (CurrentNavigation.SelectedQueue is not null
                || CurrentNavigation.SelectedTopic is not null)
            {
                return null;
            }

            return ActiveTab?.CurrentDestination is { EntityType: EntityType.Subscription } request
                ? request.TopicName
                : null;
        }
    }

    /// <summary>Gets entity segment for current entity breadcrumb.</summary>
    public string? WorkspaceEntityName
    {
        get
        {
            if (CurrentNavigation.SelectedSubscription is { } subscription)
            {
                return subscription.Name;
            }

            if (CurrentNavigation.SelectedQueue is { } queue)
            {
                return queue.Name;
            }

            if (CurrentNavigation.SelectedTopic is { } topic)
            {
                return topic.Name;
            }

            var request = ActiveTab?.CurrentDestination;
            if (request is null)
            {
                return null;
            }

            if (request.EntityType != EntityType.Subscription || string.IsNullOrWhiteSpace(request.TopicName))
            {
                return request.EntityName;
            }

            var prefix = $"{request.TopicName}/";
            return request.EntityName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? request.EntityName[prefix.Length..]
                : request.EntityName;
        }
    }

    /// <summary>Gets destination segment for current entity breadcrumb.</summary>
    public string? WorkspaceDestinationLabel
    {
        get
        {
            if (CurrentNavigation.SelectedTopic is not null
                && CurrentNavigation.SelectedSubscription is null)
            {
                return "Subscriptions";
            }

            if (CurrentNavigation.SelectedQueue is not null
                || CurrentNavigation.SelectedSubscription is not null)
            {
                return CurrentNavigation.SelectedMessageTabIndex switch
                {
                    1 => "Dead letters",
                    2 => "Sessions",
                    _ => "Active messages"
                };
            }

            return ActiveTab?.CurrentDestination?.View switch
            {
                EntityWorkspaceView.ActiveMessages => "Active messages",
                EntityWorkspaceView.DeadLetters => "Dead letters",
                EntityWorkspaceView.Sessions => "Sessions",
                EntityWorkspaceView.TopicSubscriptions => "Subscriptions",
                _ => null
            };
        }
    }

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

    private CorrelationSourceContext? GetCorrelationSourceContext()
    {
        var entityName = CurrentNavigation.CurrentEntityName;
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return null;
        }

        var subscriptionName = CurrentNavigation.CurrentSubscriptionName;
        var namespaceName = ActiveTab?.SavedConnection?.Endpoint
            ?? ActiveTab?.Namespace?.Name
            ?? CurrentNavigation.SelectedNamespace?.Name
            ?? "current-namespace";

        return new CorrelationSourceContext(
            namespaceName,
            ActiveTab?.SavedConnection?.Environment ?? ConnectionEnvironment.None,
            subscriptionName ?? entityName,
            subscriptionName == null ? "Queue" : "Subscription",
            subscriptionName == null ? null : entityName,
            subscriptionName);
    }

    private IReadOnlyList<ReplayDestination> GetReplayDestinations()
    {
        var namespaceName = ActiveTab?.SavedConnection?.Endpoint
            ?? ActiveTab?.Namespace?.Name
            ?? CurrentNavigation.SelectedNamespace?.Name
            ?? "current-namespace";
        var environment = ActiveTab?.SavedConnection?.Environment ?? ConnectionEnvironment.None;
        var scheduledConnectionContext = GetScheduledMessageConnectionContext();

        var destinations = CurrentNavigation.Queues
            .Select(queue => new ReplayDestination(
                namespaceName,
                environment,
                queue.Name,
                "Queue",
                queue.RequiresSession,
                scheduledConnectionContext))
            .ToList();

        destinations.AddRange(CurrentNavigation.Topics.Select(topic => new ReplayDestination(
            namespaceName,
            environment,
            topic.Name,
            "Topic",
            RequiresSession: false,
            scheduledConnectionContext)));

        return destinations;
    }

    // UI State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isMoreToolsExpanded;
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
    private long _namespaceNavigationGeneration;
    private CancellationTokenSource? _namespaceNavigationCts;

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
        ViewModels.Dashboard.NamespaceDashboardViewModel namespaceDashboardViewModel,
        IScheduledMessageStore? scheduledMessageStore = null,
        INamespaceTopologyService? namespaceTopologyService = null,
        IFileDialogService? fileDialogService = null,
        ICorrelationMessageCatalog? correlationMessageCatalog = null,
        IReplayAuditStore? replayAuditStore = null,
        IMessageReplayService? messageReplayService = null,
        ICorrelationRefreshDelay? correlationRefreshDelay = null,
        ICorrelationMessageComparisonService? correlationComparisonService = null,
        IScheduledMessageManagementService? scheduledMessageManagementService = null)
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
        _scheduledMessageManagementService = scheduledMessageManagementService;
        _correlationMessageCatalog = correlationMessageCatalog ?? new CorrelationMessageCatalog();

        // Initialize dashboard components
        NamespaceDashboard = namespaceDashboardViewModel;
        NamespaceDashboard.UpdateNavigation(OpenInboxDestination);
        NamespaceDashboard.UpdateOverviewSection(section =>
        {
            if (ActiveTab is not null)
            {
                ActiveTab.OverviewSection = section;
            }
        });

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
            msg => StatusMessage = msg,
            _correlationMessageCatalog,
            GetCorrelationSourceContext);

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
            () => ActiveTab?.Operations ?? _operations,
            () => CurrentNavigation.Queues,
            () => CurrentNavigation.Topics,
            () => CurrentNavigation.TopicSubscriptions,
            () => CurrentNavigation.SelectedQueue,
            () => CurrentNavigation.SelectedSubscription,
            msg => StatusMessage = msg,
            _correlationMessageCatalog,
            GetCorrelationSourceContext,
            messageReplayService,
            replayAuditStore,
            GetReplayDestinations,
            () => _fileDialogService,
            correlationRefreshDelay,
            correlationComparisonService,
            scheduledMessageManagementService,
            CloneScheduledMessageAsync,
            TimeProvider.System);

        // Initialize refactored components
        Tabs = new TabManagementViewModel(
            operationsFactory,
            preferencesService,
            connectionStorage,
            auth,
            _logSink,
            tab => ActiveTab = tab,
            _correlationMessageCatalog);

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
        NamespaceDashboard.Deactivate();
        _operations = operations;

        var namespaceId = ActiveTab?.Namespace?.Id ?? ActiveTab?.SavedConnection?.Name ?? "current-namespace";
        NamespaceDashboard.SetOperations(operations, namespaceId);
        UpdateNamespaceDashboardLifecycle();
    }

    private void UpdateNamespaceDashboardLifecycle()
    {
        if (IsNamespaceOverviewVisible)
        {
            NamespaceDashboard.Activate();
        }
        else
        {
            NamespaceDashboard.Deactivate();
        }
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
        UpdateNamespaceDashboardNavigationContext();
    }

    [RelayCommand]
    private void ToggleEntityPin(object? entity)
    {
        CurrentNavigation.TogglePin(entity);
        UpdateNamespaceDashboardNavigationContext();
    }

    [RelayCommand]
    private async Task SelectPinnedEntityAsync(PinnedEntity? pin)
    {
        if (pin == null)
        {
            return;
        }

        var entityType = pin.Type switch
        {
            PinnedEntityType.Queue => EntityType.Queue,
            PinnedEntityType.Topic => EntityType.Topic,
            PinnedEntityType.Subscription => EntityType.Subscription,
            _ => EntityType.Queue
        };
        var entityName = pin.Type == PinnedEntityType.Subscription
            ? $"{pin.TopicName}/{pin.Name}"
            : pin.Name;
        var view = entityType == EntityType.Topic
            ? EntityWorkspaceView.TopicSubscriptions
            : EntityWorkspaceView.ActiveMessages;

        await NavigateToNamespaceDestinationAsync(new NamespaceNavigationRequest(
            entityType,
            entityName,
            pin.TopicName,
            view));
    }

    private void OpenInboxDestination(NamespaceNavigationRequest request)
    {
        FireAndForget(
            NavigateToNamespaceDestinationAsync(request),
            nameof(NavigateToNamespaceDestinationAsync));
    }

    private async Task NavigateToNamespaceDestinationAsync(NamespaceNavigationRequest request)
    {
        var tab = ActiveTab;
        if (tab is null)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _namespaceNavigationGeneration);
        var navigationCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _namespaceNavigationCts, navigationCts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        tab.CurrentDestination = request;
        tab.WorkspaceMode = NamespaceWorkspaceMode.Entity;
        NotifyActiveTabDependentProperties();
        NamespaceDashboard.Deactivate();

        try
        {
            var resolved = await SelectRequestedEntityAsync(tab, request, generation, navigationCts.Token);
            if (resolved && IsCurrentNamespaceNavigation(tab, generation))
            {
                tab.RecordRecentDestination(request);
                UpdateNamespaceDashboardNavigationContext();
            }
        }
        catch (OperationCanceledException) when (navigationCts.IsCancellationRequested)
        {
            // A newer destination or Overview return superseded this request.
        }
        catch (Exception ex)
        {
            if (IsCurrentNamespaceNavigation(tab, generation))
            {
                tab.StatusMessage = $"Unable to open {request.EntityName}: {ex.Message}";
            }
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _namespaceNavigationCts, null, navigationCts) == navigationCts)
            {
                navigationCts.Dispose();
            }
        }
    }

    private async Task<bool> SelectRequestedEntityAsync(
        ConnectionTabViewModel tab,
        NamespaceNavigationRequest request,
        long generation,
        CancellationToken ct)
    {
        return request.EntityType switch
        {
            EntityType.Queue => await SelectRequestedQueueAsync(tab, request, generation, ct),
            EntityType.Topic => await SelectRequestedTopicAsync(tab, request, generation, ct),
            EntityType.Subscription => await SelectRequestedSubscriptionAsync(tab, request, generation, ct),
            _ => SetUnsupportedDestinationError(tab, request)
        };
    }

    private async Task<bool> SelectRequestedQueueAsync(
        ConnectionTabViewModel tab,
        NamespaceNavigationRequest request,
        long generation,
        CancellationToken ct)
    {
        var queue = tab.Navigation.Queues.FirstOrDefault(item =>
            string.Equals(item.Name, request.EntityName, StringComparison.OrdinalIgnoreCase));
        if (queue is null)
        {
            tab.StatusMessage = $"Queue no longer available: {request.EntityName}";
            return false;
        }

        tab.Navigation.SelectedQueue = queue;
        tab.Navigation.SelectedTopic = null;
        tab.Navigation.SelectedSubscription = null;
        tab.Navigation.SelectedEntity = queue;
        tab.Navigation.TopicSubscriptions.Clear();
        return await LoadRequestedDestinationAsync(tab, request, generation, ct);
    }

    private async Task<bool> SelectRequestedTopicAsync(
        ConnectionTabViewModel tab,
        NamespaceNavigationRequest request,
        long generation,
        CancellationToken ct)
    {
        if (request.View != EntityWorkspaceView.TopicSubscriptions)
        {
            tab.StatusMessage = $"Topic destination is not supported: {request.View}";
            return false;
        }

        var topic = tab.Navigation.Topics.FirstOrDefault(item =>
            string.Equals(item.Name, request.EntityName, StringComparison.OrdinalIgnoreCase));
        if (topic is null || tab.Operations is null)
        {
            tab.StatusMessage = $"Topic no longer available: {request.EntityName}";
            return false;
        }

        tab.Navigation.SelectedTopic = topic;
        tab.Navigation.SelectedQueue = null;
        tab.Navigation.SelectedSubscription = null;
        tab.Navigation.SelectedEntity = topic;
        tab.MessageOps.Clear();
        tab.SessionInspector.Clear();
        tab.Navigation.TopicSubscriptions.Clear();
        IsLoading = true;
        tab.StatusMessage = $"Loading subscriptions for {topic.Name}...";

        try
        {
            var subscriptions = await tab.Operations.GetSubscriptionsAsync(topic.Name, ct);
            if (!IsCurrentNamespaceNavigation(tab, generation))
            {
                return false;
            }

            foreach (var subscription in subscriptions)
            {
                tab.Navigation.TopicSubscriptions.Add(subscription);
            }

            tab.StatusMessage = $"{tab.Navigation.TopicSubscriptions.Count} subscription(s)";
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (IsCurrentNamespaceNavigation(tab, generation))
            {
                tab.StatusMessage = $"Unable to load subscriptions: {ex.Message}";
            }

            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<bool> SelectRequestedSubscriptionAsync(
        ConnectionTabViewModel tab,
        NamespaceNavigationRequest request,
        long generation,
        CancellationToken ct)
    {
        var subscriptionName = GetSubscriptionName(request);
        if (string.IsNullOrWhiteSpace(request.TopicName)
            || string.IsNullOrWhiteSpace(subscriptionName)
            || tab.Operations is null)
        {
            tab.StatusMessage = $"Subscription no longer available: {request.EntityName}";
            return false;
        }

        var subscriptions = await tab.Operations.GetSubscriptionsAsync(request.TopicName, ct);
        if (!IsCurrentNamespaceNavigation(tab, generation))
        {
            return false;
        }

        var subscription = subscriptions.FirstOrDefault(item =>
            string.Equals(item.Name, subscriptionName, StringComparison.OrdinalIgnoreCase));
        if (subscription is null)
        {
            tab.StatusMessage = $"Subscription no longer available: {request.EntityName}";
            return false;
        }

        tab.Navigation.SelectedTopic = null;
        tab.Navigation.SelectedQueue = null;
        tab.Navigation.SelectedSubscription = subscription;
        tab.Navigation.SelectedEntity = subscription;
        return await LoadRequestedDestinationAsync(tab, request, generation, ct);
    }

    private async Task<bool> LoadRequestedDestinationAsync(
        ConnectionTabViewModel tab,
        NamespaceNavigationRequest request,
        long generation,
        CancellationToken ct)
    {
        tab.MessageOps.ClearSessionScope();
        tab.SessionInspector.Clear();

        Interlocked.Increment(ref _suppressDeadLetterReload);
        try
        {
            tab.Navigation.SelectedMessageTabIndex = request.View switch
            {
                EntityWorkspaceView.ActiveMessages => 0,
                EntityWorkspaceView.DeadLetters => 1,
                EntityWorkspaceView.Sessions => 2,
                _ => 0
            };
        }
        finally
        {
            Interlocked.Decrement(ref _suppressDeadLetterReload);
        }

        if (request.View == EntityWorkspaceView.Sessions)
        {
            await tab.SessionInspector.LoadSessionsAsync();
        }
        else
        {
            var waitDeadline = DateTimeOffset.UtcNow + NamespaceNavigationLoadWaitTimeout;
            while (tab.MessageOps.IsLoadingMessages && DateTimeOffset.UtcNow < waitDeadline)
            {
                await Task.Delay(10, ct);
            }

            if (!IsCurrentNamespaceNavigation(tab, generation))
            {
                return false;
            }

            if (tab.MessageOps.IsLoadingMessages)
            {
                tab.StatusMessage = "Timed out waiting for the current message load to finish.";
                return false;
            }

            await tab.MessageOps.LoadMessagesAsync(ct);
        }

        return IsCurrentNamespaceNavigation(tab, generation);
    }

    private bool IsCurrentNamespaceNavigation(ConnectionTabViewModel tab, long generation) =>
        ReferenceEquals(ActiveTab, tab)
        && Volatile.Read(ref _namespaceNavigationGeneration) == generation;

    private static bool SetUnsupportedDestinationError(
        ConnectionTabViewModel tab,
        NamespaceNavigationRequest request)
    {
        tab.StatusMessage = $"Navigation does not support {request.EntityType}";
        return false;
    }

    private static string? GetSubscriptionName(NamespaceNavigationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TopicName))
        {
            return null;
        }

        var prefix = $"{request.TopicName}/";
        return request.EntityName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? request.EntityName[prefix.Length..]
            : request.EntityName;
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
            scheduledMessageStore: _scheduledMessageStore,
            scheduledConnectionContext: GetScheduledMessageConnectionContext(),
            subscriptionName: CurrentNavigation.CurrentSubscriptionName);

        ShowSendMessagePopup = true;
    }

    private void CloseSendMessagePopup() =>
        FireAndForget(CloseSendMessagePopupAsync(), nameof(CloseSendMessagePopupAsync));

    private async Task CloseSendMessagePopupAsync()
    {
        ShowSendMessagePopup = false;
        SendMessageViewModel = null;
        await DisposeScheduledCloneOperationsAsync();
        await CurrentMessageOps.LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task CancelSendMessageAsync()
    {
        ShowSendMessagePopup = false;
        SendMessageViewModel = null;
        await DisposeScheduledCloneOperationsAsync();
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
            scheduledMessageStore: _scheduledMessageStore,
            scheduledConnectionContext: GetScheduledMessageConnectionContext(),
            subscriptionName: CurrentNavigation.CurrentSubscriptionName);

        SendMessageViewModel.PopulateFromMessage(msg);
        ShowSendMessagePopup = true;
        CurrentMessageOps.ClearSelectedMessage();
    }

    #endregion

    private ScheduledMessageConnectionContext? GetScheduledMessageConnectionContext()
    {
        var tab = ActiveTab;
        if (tab?.SavedConnection is { } connection)
        {
            return new ScheduledMessageConnectionContext(
                connection.Id,
                connection.Name,
                connection.Endpoint ?? tab.TabSubtitle,
                connection.Environment,
                ScheduledMessageConnectionKind.ConnectionString);
        }

        var serviceBusNamespace = tab?.Namespace ?? CurrentNavigation.SelectedNamespace;
        if (serviceBusNamespace is not null)
        {
            return new ScheduledMessageConnectionContext(
                serviceBusNamespace.Id,
                serviceBusNamespace.Name,
                serviceBusNamespace.Endpoint,
                ConnectionEnvironment.None,
                ScheduledMessageConnectionKind.AzureCredential,
                serviceBusNamespace.Id);
        }

        return null;
    }

    private async Task CloneScheduledMessageAsync(
        ScheduledMessageResolvedEntry resolved,
        ScheduledMessagePayload payload)
    {
        IServiceBusOperations? operations = null;
        var entry = resolved.Entry;
        if (entry.ConnectionKind == ScheduledMessageConnectionKind.ConnectionString)
        {
            var connection = await _connectionStorage.GetConnectionAsync(entry.ConnectionId);
            if (connection is not null &&
                (connection.IsNamespaceLevel ||
                 string.Equals(connection.EntityName, entry.EntityName, StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(
                    NormalizeNamespaceEndpoint(connection.Endpoint),
                    NormalizeNamespaceEndpoint(entry.NamespaceEndpoint),
                    StringComparison.OrdinalIgnoreCase))
            {
                operations = _operationsFactory.CreateFromConnectionString(connection.ConnectionString);
            }
        }
        else if (_auth.IsAuthenticated && _auth.Credential is not null &&
                 !string.IsNullOrWhiteSpace(entry.NamespaceResourceId))
        {
            operations = _operationsFactory.CreateFromAzureCredential(
                entry.NamespaceEndpoint,
                entry.NamespaceResourceId,
                _auth.Credential);
        }

        if (operations is null)
        {
            StatusMessage = "The indexed connection is unavailable";
            return;
        }

        if (_scheduledCloneOperations is not null)
        {
            await _scheduledCloneOperations.DisposeAsync();
        }
        _scheduledCloneOperations = operations;
        SendMessageViewModel = new SendMessageViewModel(
            operations,
            entry.EntityName,
            CloseSendMessagePopup,
            status => StatusMessage = status,
            _fileDialogService,
            scheduledMessageStore: _scheduledMessageStore,
            scheduledConnectionContext: new ScheduledMessageConnectionContext(
                entry.ConnectionId,
                entry.ConnectionName,
                entry.NamespaceEndpoint,
                entry.Environment,
                entry.ConnectionKind,
                entry.NamespaceResourceId),
            subscriptionName: entry.SubscriptionName);
        SendMessageViewModel.PopulateFromScheduledPayload(payload);
        ShowSendMessagePopup = true;
    }

    private static string NormalizeNamespaceEndpoint(string? endpoint) =>
        (endpoint ?? "").Replace("sb://", "", StringComparison.OrdinalIgnoreCase).TrimEnd('/');

    private async Task DisposeScheduledCloneOperationsAsync()
    {
        if (_scheduledCloneOperations is null)
        {
            return;
        }
        var operations = _scheduledCloneOperations;
        _scheduledCloneOperations = null;
        await operations.DisposeAsync();
    }

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
            "Open Overview",
            "Namespace triage, search, metrics, and recent work",
            "Features",
            "LayoutDashboard",
            Run(OpenOverview));

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
    private void CloseLiveStream()
    {
        FeaturePanels.CloseLiveStream();
        UpdateNamespaceDashboardLifecycle();
    }

    [RelayCommand]
    private async Task OpenCorrelationExplorer()
    {
        NamespaceDashboard.Deactivate();
        await FeaturePanels.OpenCorrelationExplorer();
    }

    [RelayCommand]
    private void CloseCorrelationExplorer()
    {
        FeaturePanels.CloseCorrelationExplorer();
        UpdateNamespaceDashboardLifecycle();
    }

    [RelayCommand]
    private Task OpenScheduledMessages()
    {
        NamespaceDashboard.Deactivate();
        return FeaturePanels.OpenScheduledMessages();
    }

    [RelayCommand]
    private void CloseScheduledMessages()
    {
        FeaturePanels.CloseScheduledMessages();
        UpdateNamespaceDashboardLifecycle();
    }

    [RelayCommand]
    private void OpenOverview()
    {
        if (ActiveTab is null)
        {
            return;
        }

        CancelNamespaceNavigation();
        FeaturePanels.CloseAll();
        ActiveTab.WorkspaceMode = NamespaceWorkspaceMode.Overview;
        NotifyActiveTabDependentProperties();
        UpdateNamespaceDashboardLifecycle();
    }

    [RelayCommand]
    private void CloseOverview()
    {
        if (ActiveTab is null)
        {
            return;
        }

        ActiveTab.WorkspaceMode = NamespaceWorkspaceMode.Entity;
        NotifyActiveTabDependentProperties();
        UpdateNamespaceDashboardLifecycle();
    }

    [RelayCommand]
    private void OpenAlerts()
    {
        NamespaceDashboard.Deactivate();
        FeaturePanels.OpenAlerts();
    }

    [RelayCommand]
    private void CloseAlerts()
    {
        FeaturePanels.CloseAlerts();
        UpdateNamespaceDashboardLifecycle();
    }

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
        CancelNamespaceNavigation();

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
        UpdateNamespaceDashboardNavigationContext();

        if (CurrentNavigation.IsSessionInspectorTabSelected)
        {
            FireAndForget(CurrentSessionInspector.LoadSessionsAsync(), nameof(SessionInspectorViewModel.LoadSessionsAsync));
        }

    }

    private void OnActiveTabNavigationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NavigationState.SelectedQueue)
            or nameof(NavigationState.SelectedTopic)
            or nameof(NavigationState.SelectedSubscription)
            or nameof(NavigationState.SelectedMessageTabIndex))
        {
            OnPropertyChanged(nameof(WorkspaceTopicName));
            OnPropertyChanged(nameof(WorkspaceEntityName));
            OnPropertyChanged(nameof(WorkspaceDestinationLabel));
        }

        if (e.PropertyName == nameof(NavigationState.SelectedMessageTabIndex)
            && Volatile.Read(ref _suppressDeadLetterReload) != 0)
        {
            return;
        }

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
        if (e.PropertyName is nameof(ConnectionTabViewModel.IsConnected)
            or nameof(ConnectionTabViewModel.Mode)
            or nameof(ConnectionTabViewModel.WorkspaceMode))
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
            SetOperations(tab?.IsConnected == true ? tab.Operations : null);
            UpdateNamespaceDashboardNavigationContext();
        }
        else if (e.PropertyName == nameof(ConnectionTabViewModel.WorkspaceMode))
        {
            UpdateNamespaceDashboardLifecycle();
        }
        else if (e.PropertyName == nameof(ConnectionTabViewModel.CurrentDestination))
        {
            OnPropertyChanged(nameof(WorkspaceTopicName));
            OnPropertyChanged(nameof(WorkspaceEntityName));
            OnPropertyChanged(nameof(WorkspaceDestinationLabel));
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
        OnPropertyChanged(nameof(IsNamespaceOverviewVisible));
        OnPropertyChanged(nameof(IsAzureEntityWorkspaceVisible));
        OnPropertyChanged(nameof(IsConnectionStringEntityWorkspaceVisible));
        OnPropertyChanged(nameof(WorkspaceTopicName));
        OnPropertyChanged(nameof(WorkspaceEntityName));
        OnPropertyChanged(nameof(WorkspaceDestinationLabel));
        OnPropertyChanged(nameof(ActiveWorkspaceModeLabel));
        OnPropertyChanged(nameof(IsCurrentEntityPaneVisible));
        OnPropertyChanged(nameof(ShowWelcome));
        OnPropertyChanged(nameof(ShowNamespaceSelectionPrompt));
        OnPropertyChanged(nameof(CurrentNavigation));
        OnPropertyChanged(nameof(CurrentMessageOps));
        OnPropertyChanged(nameof(CurrentSessionInspector));
    }

    private void UpdateNamespaceDashboardNavigationContext()
    {
        var tab = ActiveTab;
        if (tab is null)
        {
            NamespaceDashboard.SetNavigationContext([], [], [], [], []);
            return;
        }

        NamespaceDashboard.SetNavigationContext(
            tab.Navigation.Queues,
            tab.Navigation.Topics,
            tab.Navigation.TopicSubscriptions,
            tab.Navigation.PinnedEntities,
            tab.RecentDestinations);
        NamespaceDashboard.SelectedSection = tab.OverviewSection;
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

    private void CancelNamespaceNavigation()
    {
        Interlocked.Increment(ref _namespaceNavigationGeneration);
        var navigationCts = Interlocked.Exchange(ref _namespaceNavigationCts, null);
        navigationCts?.Cancel();
        navigationCts?.Dispose();
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

        CancelNamespaceNavigation();
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;

        // Dispose the log viewer to unsubscribe from events
        LogViewer?.Dispose();
        Terminal?.Dispose();
        FeaturePanels.CloseCorrelationExplorer();
        if (_scheduledCloneOperations is IDisposable cloneDisposable)
        {
            cloneDisposable.Dispose();
            _scheduledCloneOperations = null;
        }

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
        FeaturePanels.CloseCorrelationExplorer();
        await DisposeScheduledCloneOperationsAsync();

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
