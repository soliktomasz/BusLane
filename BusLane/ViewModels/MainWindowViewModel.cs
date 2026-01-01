using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

using Services.Abstractions;
using Services.Auth;
using Services.Infrastructure;
using Services.Monitoring;
using Services.ServiceBus;
using Services.Storage;

public enum ConnectionMode
{
    None,
    AzureAccount,
    ConnectionString
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAzureAuthService _auth;
    private readonly IServiceBusService _serviceBus;
    private readonly IConnectionStorageService _connectionStorage;
    private readonly IConnectionStringService _connectionStringService;
    private readonly IVersionService _versionService;
    private readonly ILiveStreamService _liveStreamService;
    private readonly IMetricsService _metricsService;
    private readonly IAlertService _alertService;
    private readonly INotificationService _notificationService;
    private IFileDialogService? _fileDialogService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAzureSections))]
    private bool _isAuthenticated;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMessages;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _selectedSubscriptionId;
    [ObservableProperty] private AzureSubscription? _selectedAzureSubscription;
    [ObservableProperty] private ServiceBusNamespace? _selectedNamespace;
    [ObservableProperty] private object? _selectedEntity;
    [ObservableProperty] private QueueInfo? _selectedQueue;
    [ObservableProperty] private TopicInfo? _selectedTopic;
    [ObservableProperty] private SubscriptionInfo? _selectedSubscription;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedMessageBody))]
    [NotifyPropertyChangedFor(nameof(IsMessageBodyJson))]
    [NotifyPropertyChangedFor(nameof(FormattedApplicationProperties))]
    private MessageInfo? _selectedMessage;
    [ObservableProperty] private bool _showDeadLetter;
    [ObservableProperty] private int _selectedMessageTabIndex; // 0 = Active, 1 = Dead Letter
    [ObservableProperty] private string _newMessageBody = "";
    [ObservableProperty] private bool _showStatusPopup;
    [ObservableProperty] private bool _showSendMessagePopup;
    [ObservableProperty] private SendMessageViewModel? _sendMessageViewModel;

    // Connection mode properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAzureSections))]
    private ConnectionMode _currentMode = ConnectionMode.None;
    [ObservableProperty] private bool _showConnectionLibrary;
    [ObservableProperty] private ConnectionLibraryViewModel? _connectionLibraryViewModel;
    [ObservableProperty] private SavedConnection? _activeConnection;

    // Settings properties
    [ObservableProperty] private bool _showSettings;
    [ObservableProperty] private SettingsViewModel? _settingsViewModel;

    // Confirmation dialog properties
    [ObservableProperty] private bool _showConfirmDialog;
    [ObservableProperty] private string _confirmDialogTitle = "";
    [ObservableProperty] private string _confirmDialogMessage = "";
    [ObservableProperty] private string _confirmDialogConfirmText = "Confirm";
    private Func<Task>? _confirmDialogAction;

    // Auto-refresh timer
    private System.Timers.Timer? _autoRefreshTimer;

    // Live Stream, Charts, and Alerts properties
    [ObservableProperty] private bool _showLiveStream;
    [ObservableProperty] private bool _showCharts;
    [ObservableProperty] private bool _showAlerts;
    [ObservableProperty] private LiveStreamViewModel? _liveStreamViewModel;
    [ObservableProperty] private ChartsViewModel? _chartsViewModel;
    [ObservableProperty] private AlertsViewModel? _alertsViewModel;
    [ObservableProperty] private int _activeAlertCount;

    // Multi-select support for bulk operations
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMessages))]
    [NotifyPropertyChangedFor(nameof(SelectedMessagesCount))]
    [NotifyPropertyChangedFor(nameof(CanResubmitDeadLetters))]
    private bool _isMultiSelectMode;

    // Selection version counter to force UI refresh when selection changes
    [ObservableProperty]
    private int _selectionVersion;

    partial void OnIsMultiSelectModeChanged(bool value)
    {
        if (!value)
        {
            SelectedMessages.Clear();
            SelectionVersion++;
        }
    }

    public ObservableCollection<AzureSubscription> Subscriptions { get; } = [];
    public ObservableCollection<ServiceBusNamespace> Namespaces { get; } = [];
    public ObservableCollection<QueueInfo> Queues { get; } = [];
    public ObservableCollection<TopicInfo> Topics { get; } = [];
    public ObservableCollection<SubscriptionInfo> TopicSubscriptions { get; } = [];
    public ObservableCollection<MessageInfo> Messages { get; } = [];
    public ObservableCollection<MessageInfo> SelectedMessages { get; } = [];
    public ObservableCollection<SavedConnection> SavedConnections { get; } = [];
    public ObservableCollection<SavedConnection> FavoriteConnections { get; } = [];

    // Multi-select computed properties
    public bool HasSelectedMessages => SelectedMessages.Count > 0;
    public int SelectedMessagesCount => SelectedMessages.Count;
    public bool CanResubmitDeadLetters => HasSelectedMessages && ShowDeadLetter;

    // Computed properties for visibility bindings (Count doesn't notify on collection changes)
    public bool HasQueues => Queues.Count > 0;
    public bool HasTopics => Topics.Count > 0;
    public bool HasFavoriteConnections => FavoriteConnections.Count > 0;

    // Show Azure sections only when authenticated AND in Azure account mode (not using saved connection)
    public bool ShowAzureSections => IsAuthenticated && CurrentMode == ConnectionMode.AzureAccount;

    // Total dead letter count across all queues and topic subscriptions
    public long TotalDeadLetterCount => Queues.Sum(q => q.DeadLetterCount) + TopicSubscriptions.Sum(s => s.DeadLetterCount);
    public bool HasDeadLetters => TotalDeadLetterCount > 0;

    // Settings-driven computed properties
    public bool ShowDeadLetterBadges => Preferences.ShowDeadLetterBadges;
    public bool EnableMessagePreview => Preferences.EnableMessagePreview;

    // Sorting properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortButtonText))]
    private bool _sortDescending = true; // Default: newest first

    public string SortButtonText => SortDescending ? "↓ Newest" : "↑ Oldest";

    // Search properties
    [ObservableProperty]
    private string _messageSearchText = "";

    partial void OnMessageSearchTextChanged(string value)
    {
        ApplyMessageFilter();
    }

    public ObservableCollection<MessageInfo> FilteredMessages { get; } = [];

    private void ApplyMessageFilter()
    {
        FilteredMessages.Clear();

        var filtered = string.IsNullOrWhiteSpace(MessageSearchText)
            ? Messages
            : Messages.Where(m =>
                (m.MessageId?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Body?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.CorrelationId?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Subject?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.DeadLetterReason?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                m.SequenceNumber.ToString().Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var msg in filtered)
            FilteredMessages.Add(msg);
    }

    [RelayCommand]
    private void ClearMessageSearch()
    {
        MessageSearchText = "";
    }

    // Computed properties for message body formatting
    public bool IsMessageBodyJson
    {
        get
        {
            if (SelectedMessage?.Body == null) return false;
            var trimmed = SelectedMessage.Body.Trim();
            return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                   (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
        }
    }

    public string? FormattedMessageBody
    {
        get
        {
            if (SelectedMessage?.Body == null) return null;
            if (!IsMessageBodyJson) return SelectedMessage.Body;

            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(SelectedMessage.Body);
                return System.Text.Json.JsonSerializer.Serialize(jsonDoc.RootElement,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return SelectedMessage.Body;
            }
        }
    }

    public string? FormattedApplicationProperties
    {
        get
        {
            if (SelectedMessage?.Properties == null || SelectedMessage.Properties.Count == 0)
                return null;

            try
            {
                return System.Text.Json.JsonSerializer.Serialize(SelectedMessage.Properties,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return string.Join("\n", SelectedMessage.Properties.Select(p => $"{p.Key}: {p.Value}"));
            }
        }
    }

    /// <summary>
    /// Gets the application version for display in the UI.
    /// </summary>
    public string AppVersion => _versionService.DisplayVersion;

    public MainWindowViewModel(
        IAzureAuthService auth,
        IServiceBusService serviceBus,
        IConnectionStorageService connectionStorage,
        IConnectionStringService connectionStringService,
        IVersionService versionService,
        ILiveStreamService liveStreamService,
        IMetricsService metricsService,
        IAlertService alertService,
        INotificationService notificationService,
        IFileDialogService? fileDialogService = null)
    {
        _auth = auth;
        _serviceBus = serviceBus;
        _connectionStorage = connectionStorage;
        _connectionStringService = connectionStringService;
        _versionService = versionService;
        _liveStreamService = liveStreamService;
        _metricsService = metricsService;
        _alertService = alertService;
        _notificationService = notificationService;
        _fileDialogService = fileDialogService;
        _auth.AuthenticationChanged += (_, authenticated) => IsAuthenticated = authenticated;

        // Subscribe to alert events
        _alertService.AlertTriggered += OnAlertTriggered;
        _alertService.AlertsChanged += OnAlertsChanged;
        ActiveAlertCount = _alertService.ActiveAlerts.Count(a => !a.IsAcknowledged);

        // Subscribe to collection changes to notify visibility properties
        Queues.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasQueues));
            OnPropertyChanged(nameof(TotalDeadLetterCount));
            OnPropertyChanged(nameof(HasDeadLetters));
        };
        Topics.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTopics));
        TopicSubscriptions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TotalDeadLetterCount));
            OnPropertyChanged(nameof(HasDeadLetters));
        };
        FavoriteConnections.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFavoriteConnections));
        SelectedMessages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSelectedMessages));
            OnPropertyChanged(nameof(SelectedMessagesCount));
            OnPropertyChanged(nameof(CanResubmitDeadLetters));
        };

        // Initialize auto-refresh timer
        InitializeAutoRefreshTimer();
    }

    /// <summary>
    /// Sets the file dialog service. Called after the main window is created.
    /// </summary>
    public void SetFileDialogService(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
    }

    private void OnAlertTriggered(object? sender, Models.AlertEvent alert)
    {
        // Show system notification
        _notificationService.ShowAlertNotification(alert);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ActiveAlertCount = _alertService.ActiveAlerts.Count(a => !a.IsAcknowledged);
            StatusMessage = $"Alert: {alert.Rule.Name} - {alert.EntityName}";
            ShowStatusPopup = true;
        });
    }

    private void OnAlertsChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ActiveAlertCount = _alertService.ActiveAlerts.Count(a => !a.IsAcknowledged);
        });
    }

    private void InitializeAutoRefreshTimer()
    {
        _autoRefreshTimer = new System.Timers.Timer();
        _autoRefreshTimer.Elapsed += async (_, _) =>
        {
            if (Preferences.AutoRefreshMessages && (SelectedQueue != null || SelectedSubscription != null))
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await LoadMessagesAsync();
                });
            }

            // Always evaluate alerts when auto-refresh is enabled and we have queues/subscriptions loaded
            if (Queues.Count > 0 || TopicSubscriptions.Count > 0)
            {
                await _alertService.EvaluateAlertsAsync(Queues, TopicSubscriptions);
            }
        };
        UpdateAutoRefreshTimer();
    }

    /// <summary>
    /// Updates the auto-refresh timer based on current preferences.
    /// Call this after settings are saved.
    /// </summary>
    public void UpdateAutoRefreshTimer()
    {
        if (_autoRefreshTimer == null) return;

        if (Preferences.AutoRefreshMessages)
        {
            _autoRefreshTimer.Interval = Preferences.AutoRefreshIntervalSeconds * 1000;
            _autoRefreshTimer.Start();
        }
        else
        {
            _autoRefreshTimer.Stop();
        }
    }

    /// <summary>
    /// Notifies that settings-driven properties have changed.
    /// Call this after settings are saved.
    /// </summary>
    public void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(ShowDeadLetterBadges));
        OnPropertyChanged(nameof(EnableMessagePreview));
        UpdateAutoRefreshTimer();
    }

    #region Live Stream, Charts, and Alerts Commands

    [RelayCommand]
    private async Task OpenLiveStream()
    {
        LiveStreamViewModel = new LiveStreamViewModel(_liveStreamService);

        // Pass available entities to the LiveStreamViewModel
        var endpoint = SelectedNamespace?.Endpoint ?? ActiveConnection?.Endpoint;
        LiveStreamViewModel.SetAvailableEntities(endpoint, Queues, Topics);

        ShowLiveStream = true;
        ShowCharts = false;
        ShowAlerts = false;

        // Automatically start streaming for the currently selected entity
        await StartLiveStreamForSelectedEntity();
    }

    [RelayCommand]
    private void CloseLiveStream()
    {
        ShowLiveStream = false;
        _ = LiveStreamViewModel?.DisposeAsync();
        LiveStreamViewModel = null;
    }

    [RelayCommand]
    private void OpenCharts()
    {
        ChartsViewModel = new ChartsViewModel(_metricsService);
        // Record current metrics for charts
        ChartsViewModel.RecordCurrentMetrics(Queues, TopicSubscriptions);
        ChartsViewModel.UpdateEntityDistribution(Queues, TopicSubscriptions);
        ChartsViewModel.UpdateComparisonChart(Queues, TopicSubscriptions);
        ShowCharts = true;
        ShowLiveStream = false;
        ShowAlerts = false;
    }

    [RelayCommand]
    private void CloseCharts()
    {
        ChartsViewModel = null;
        ShowCharts = false;
    }

    [RelayCommand]
    private void OpenAlerts()
    {
        AlertsViewModel = new AlertsViewModel(_alertService, _notificationService, () => ShowAlerts = false);
        ShowAlerts = true;
        ShowLiveStream = false;
        ShowCharts = false;
    }

    [RelayCommand]
    private void CloseAlerts()
    {
        ShowAlerts = false;
        AlertsViewModel = null;
    }

    [RelayCommand]
    private async Task StartLiveStreamForSelectedEntity()
    {
        if (LiveStreamViewModel == null) return;

        var endpoint = SelectedNamespace?.Endpoint ?? ActiveConnection?.Endpoint;
        if (string.IsNullOrEmpty(endpoint)) return;

        if (SelectedQueue != null)
        {
            await LiveStreamViewModel.StartQueueAsync(endpoint, SelectedQueue.Name);
        }
        else if (SelectedSubscription != null)
        {
            // Use the subscription's TopicName property directly instead of relying on SelectedTopic
            await LiveStreamViewModel.StartSubscriptionAsync(endpoint, SelectedSubscription.TopicName, SelectedSubscription.Name);
        }
    }

    [RelayCommand]
    private async Task EvaluateAlerts()
    {
        await _alertService.EvaluateAlertsAsync(Queues, TopicSubscriptions);
    }

    #endregion

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading saved connections...";

        try
        {
            // Load saved connections first
            await LoadSavedConnectionsAsync();

            StatusMessage = SavedConnections.Count > 0
                ? "Select a saved connection or sign in with Azure"
                : "Add a connection or sign in with Azure to get started";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ready - {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSavedConnectionsAsync()
    {
        SavedConnections.Clear();
        FavoriteConnections.Clear();
        var connections = await _connectionStorage.GetConnectionsAsync();
        foreach (var conn in connections.OrderByDescending(c => c.CreatedAt))
        {
            SavedConnections.Add(conn);
            if (conn.IsFavorite)
            {
                FavoriteConnections.Add(conn);
            }
        }
        OnPropertyChanged(nameof(HasFavoriteConnections));
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsLoading = true;
        StatusMessage = "Signing in to Azure...";

        try
        {
            if (await _auth.LoginAsync())
            {
                CurrentMode = ConnectionMode.AzureAccount;
                ActiveConnection = null;
                StatusMessage = "Loading subscriptions...";
                await LoadSubscriptionsAsync();
                StatusMessage = "Ready";
            }
            else
            {
                StatusMessage = "Sign in failed";
            }
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
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        CurrentMode = ConnectionMode.None;
        ActiveConnection = null;
        Subscriptions.Clear();
        Namespaces.Clear();
        Queues.Clear();
        Topics.Clear();
        TopicSubscriptions.Clear();
        Messages.Clear();
        SelectedMessages.Clear();
        IsMultiSelectMode = false;
        SelectedNamespace = null;
        SelectedEntity = null;
        SelectedQueue = null;
        SelectedTopic = null;
        SelectedSubscription = null;
        SelectedMessage = null;
        StatusMessage = "Disconnected";
    }

    private async Task LoadSubscriptionsAsync()
    {
        Subscriptions.Clear();
        foreach (var sub in await _serviceBus.GetSubscriptionsAsync())
            Subscriptions.Add(sub);

        if (Subscriptions.Count > 0)
            SelectedAzureSubscription = Subscriptions[0];
    }

    partial void OnSelectedSubscriptionIdChanged(string? value)
    {
        if (value != null)
            _ = LoadNamespacesAsync(value);
    }

    partial void OnSelectedAzureSubscriptionChanged(AzureSubscription? value)
    {
        if (value != null)
            SelectedSubscriptionId = value.Id;
    }

    partial void OnSelectedMessageTabIndexChanged(int value)
    {
        // Sync ShowDeadLetter with tab index (0 = Active, 1 = Dead Letter)
        ShowDeadLetter = value == 1;
        _ = LoadMessagesAsync();
    }

    private async Task LoadNamespacesAsync(string subscriptionId)
    {
        IsLoading = true;
        StatusMessage = "Loading namespaces...";

        try
        {
            Namespaces.Clear();
            foreach (var ns in await _serviceBus.GetNamespacesAsync(subscriptionId))
                Namespaces.Add(ns);
            StatusMessage = $"Found {Namespaces.Count} namespace(s)";
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
    private async Task SelectNamespaceAsync(ServiceBusNamespace ns)
    {
        SelectedNamespace = ns;
        IsLoading = true;
        StatusMessage = $"Loading {ns.Name}...";

        try
        {
            Queues.Clear();
            Topics.Clear();
            TopicSubscriptions.Clear();
            Messages.Clear();
            SelectedQueue = null;
            SelectedTopic = null;
            SelectedSubscription = null;
            SelectedMessage = null;

            foreach (var q in await _serviceBus.GetQueuesAsync(ns.Id))
                Queues.Add(q);

            foreach (var t in await _serviceBus.GetTopicsAsync(ns.Id))
                Topics.Add(t);

            StatusMessage = $"{Queues.Count} queue(s), {Topics.Count} topic(s)";

            // Evaluate alerts after loading queues and topics
            await _alertService.EvaluateAlertsAsync(Queues, TopicSubscriptions);
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
    private async Task SelectQueueAsync(QueueInfo queue)
    {
        SelectedQueue = queue;
        SelectedTopic = null;
        SelectedSubscription = null;
        SelectedEntity = queue;
        TopicSubscriptions.Clear();
        await LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task SelectTopicAsync(TopicInfo topic)
    {
        if (SelectedNamespace == null) return;

        SelectedTopic = topic;
        SelectedQueue = null;
        SelectedSubscription = null;
        SelectedEntity = topic;
        Messages.Clear();
        TopicSubscriptions.Clear();

        IsLoading = true;
        StatusMessage = $"Loading subscriptions for {topic.Name}...";

        try
        {
            foreach (var sub in await _serviceBus.GetSubscriptionsAsync(SelectedNamespace.Id, topic.Name))
                TopicSubscriptions.Add(sub);

            StatusMessage = $"{TopicSubscriptions.Count} subscription(s)";

            // Evaluate alerts after loading subscriptions
            await _alertService.EvaluateAlertsAsync(Queues, TopicSubscriptions);
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
        SelectedSubscription = sub;
        SelectedQueue = null;
        SelectedEntity = sub;
        await LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        // Route to appropriate method based on current mode
        if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
        {
            await LoadMessagesForConnectionAsync();
            return;
        }

        if (SelectedNamespace == null) return;

        string? entityName = null;
        string? subscription = null;
        bool requiresSession = false;

        if (SelectedQueue != null)
        {
            entityName = SelectedQueue.Name;
            requiresSession = SelectedQueue.RequiresSession;
        }
        else if (SelectedSubscription != null)
        {
            entityName = SelectedSubscription.TopicName;
            subscription = SelectedSubscription.Name;
            requiresSession = SelectedSubscription.RequiresSession;
        }

        if (entityName == null) return;

        IsLoadingMessages = true;
        StatusMessage = "Loading messages...";
        MessageSearchText = ""; // Clear search when loading new messages
        SelectedMessages.Clear(); // Clear multi-select when loading new messages

        try
        {
            Messages.Clear();
            var msgs = await _serviceBus.PeekMessagesAsync(
                SelectedNamespace.Endpoint, entityName, subscription, Preferences.DefaultMessageCount, ShowDeadLetter, requiresSession
            );

            // Apply sorting
            var sortedMsgs = SortDescending
                ? msgs.OrderByDescending(m => m.EnqueuedTime)
                : msgs.OrderBy(m => m.EnqueuedTime);

            foreach (var m in sortedMsgs)
                Messages.Add(m);

            ApplyMessageFilter(); // Apply filter after loading
            StatusMessage = $"{Messages.Count} message(s)";
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityNotFound)
        {
            StatusMessage = $"Error: Entity '{entityName}' not found. Ensure you have 'Azure Service Bus Data Receiver' role assigned at the namespace or entity level.";
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            StatusMessage = $"Error: {sbEx.Reason} - {sbEx.Message}. If unauthorized, ensure you have 'Azure Service Bus Data Receiver' role assigned.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingMessages = false;
        }
    }


    [RelayCommand]
    private void OpenSendMessagePopup()
    {
        string? entityName = SelectedQueue?.Name ?? SelectedTopic?.Name;
        if (entityName == null) return;

        if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
        {
            // Connection string mode
            SendMessageViewModel = new SendMessageViewModel(
                _connectionStringService,
                ActiveConnection.ConnectionString,
                entityName,
                CloseSendMessagePopup,
                msg => StatusMessage = msg,
                _fileDialogService
            );
        }
        else if (SelectedNamespace != null)
        {
            // Azure account mode
            SendMessageViewModel = new SendMessageViewModel(
                _serviceBus,
                SelectedNamespace.Endpoint,
                entityName,
                CloseSendMessagePopup,
                msg => StatusMessage = msg,
                _fileDialogService
            );
        }
        else
        {
            return;
        }

        ShowSendMessagePopup = true;
    }

    private async void CloseSendMessagePopup()
    {
        ShowSendMessagePopup = false;
        SendMessageViewModel = null;
        await LoadMessagesAsync();
    }

    [RelayCommand]
    private void CancelSendMessage()
    {
        ShowSendMessagePopup = false;
        SendMessageViewModel = null;
    }

    [RelayCommand]
    private async Task PurgeMessagesAsync()
    {
        string? entityName = null;
        string? subscription = null;

        if (SelectedQueue != null)
        {
            entityName = SelectedQueue.Name;
        }
        else if (SelectedSubscription != null)
        {
            entityName = SelectedSubscription.TopicName;
            subscription = SelectedSubscription.Name;
        }

        if (entityName == null) return;

        // Check if confirmation is required
        if (Preferences.ConfirmBeforePurge)
        {
            var queueType = ShowDeadLetter ? "dead letter queue" : "queue";
            var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
            ShowConfirmation(
                "Confirm Purge",
                $"Are you sure you want to purge all messages from the {queueType} of '{targetName}'? This action cannot be undone.",
                "Purge",
                async () => await ExecutePurgeAsync(entityName, subscription)
            );
        }
        else
        {
            await ExecutePurgeAsync(entityName, subscription);
        }
    }

    private async Task ExecutePurgeAsync(string entityName, string? subscription)
    {
        IsLoading = true;
        StatusMessage = "Purging messages...";

        try
        {
            if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
            {
                await _connectionStringService.PurgeMessagesAsync(
                    ActiveConnection.ConnectionString, entityName, subscription, ShowDeadLetter
                );
            }
            else if (SelectedNamespace != null)
            {
                await _serviceBus.PurgeMessagesAsync(
                    SelectedNamespace.Endpoint, entityName, subscription, ShowDeadLetter
                );
            }
            else
            {
                return;
            }

            StatusMessage = "Purge complete";
            await LoadMessagesAsync();
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
    private async Task RefreshAsync()
    {
        if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
        {
            await ConnectToSavedConnectionAsync(ActiveConnection);
        }
        else if (SelectedNamespace != null)
        {
            await SelectNamespaceAsync(SelectedNamespace);
        }
    }

    [RelayCommand]
    private void SelectMessage(MessageInfo message)
    {
        SelectedMessage = message;
    }

    [RelayCommand]
    private void ClearSelectedMessage()
    {
        SelectedMessage = null;
    }

    [RelayCommand]
    private async Task ResendMessageAsync(MessageInfo? message = null)
    {
        var msg = message ?? SelectedMessage;
        if (msg == null) return;

        string? entityName = SelectedQueue?.Name ?? SelectedTopic?.Name;
        if (entityName == null) return;

        IsLoading = true;
        StatusMessage = "Resending message...";

        try
        {
            var properties = msg.Properties.ToDictionary(p => p.Key, p => p.Value);

            if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
            {
                await _connectionStringService.SendMessageAsync(
                    ActiveConnection.ConnectionString,
                    entityName,
                    msg.Body,
                    properties,
                    msg.ContentType,
                    msg.CorrelationId,
                    null, // Generate new MessageId
                    msg.SessionId,
                    msg.Subject,
                    msg.To,
                    msg.ReplyTo,
                    msg.ReplyToSessionId,
                    msg.PartitionKey,
                    msg.TimeToLive,
                    null // No scheduled enqueue time for resend
                );
            }
            else if (SelectedNamespace != null)
            {
                await _serviceBus.SendMessageAsync(
                    SelectedNamespace.Endpoint,
                    entityName,
                    msg.Body,
                    properties,
                    msg.ContentType,
                    msg.CorrelationId,
                    null, // Generate new MessageId
                    msg.SessionId,
                    msg.Subject,
                    msg.To,
                    msg.ReplyTo,
                    msg.ReplyToSessionId,
                    msg.PartitionKey,
                    msg.TimeToLive,
                    null // No scheduled enqueue time for resend
                );
            }

            StatusMessage = "Message resent successfully";
            await LoadMessagesAsync();
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
        var msg = message ?? SelectedMessage;
        if (msg == null) return;

        string? entityName = SelectedQueue?.Name ?? SelectedTopic?.Name;
        if (entityName == null) return;

        // Create the SendMessageViewModel and pre-populate with the message data
        if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
        {
            SendMessageViewModel = new SendMessageViewModel(
                _connectionStringService,
                ActiveConnection.ConnectionString,
                entityName,
                CloseSendMessagePopup,
                status => StatusMessage = status,
                _fileDialogService
            );
        }
        else if (SelectedNamespace != null)
        {
            SendMessageViewModel = new SendMessageViewModel(
                _serviceBus,
                SelectedNamespace.Endpoint,
                entityName,
                CloseSendMessagePopup,
                status => StatusMessage = status,
                _fileDialogService
            );
        }
        else
        {
            return;
        }

        // Pre-populate with message data
        SendMessageViewModel!.PopulateFromMessage(msg);
        ShowSendMessagePopup = true;

        // Clear selected message to close the detail dialog
        SelectedMessage = null;
    }

    /// <summary>
    /// File type filter for JSON files.
    /// </summary>
    private static readonly Avalonia.Platform.Storage.FilePickerFileType JsonFileType = new("JSON Files")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" }
    };

    [RelayCommand]
    private async Task ExportMessageAsync(MessageInfo? message = null)
    {
        var msg = message ?? SelectedMessage;
        if (msg == null) return;

        if (_fileDialogService == null)
        {
            StatusMessage = "File dialog service not available";
            return;
        }

        try
        {
            var safeName = string.Join("_", (msg.MessageId ?? "message").Split(Path.GetInvalidFileNameChars()));
            var defaultFileName = $"Message_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await _fileDialogService.SaveFileAsync(
                "Export Message",
                defaultFileName,
                new[] { JsonFileType });

            if (string.IsNullOrEmpty(filePath)) return;

            // Convert MessageInfo to a SavedMessage-compatible format for export
            var exportMessage = new Models.SavedMessage
            {
                Name = $"Exported: {msg.MessageId}",
                Body = msg.Body,
                ContentType = msg.ContentType,
                CorrelationId = msg.CorrelationId,
                MessageId = msg.MessageId,
                SessionId = msg.SessionId,
                Subject = msg.Subject,
                To = msg.To,
                ReplyTo = msg.ReplyTo,
                ReplyToSessionId = msg.ReplyToSessionId,
                PartitionKey = msg.PartitionKey,
                TimeToLive = msg.TimeToLive,
                ScheduledEnqueueTime = msg.ScheduledEnqueueTime,
                CustomProperties = msg.Properties?.ToDictionary(
                    p => p.Key,
                    p => p.Value?.ToString() ?? "") ?? new Dictionary<string, string>(),
                CreatedAt = DateTime.UtcNow
            };

            var exportContainer = new Models.MessageExportContainer
            {
                Description = $"Exported message from {SelectedQueue?.Name ?? SelectedSubscription?.Name}",
                Messages = new List<Models.SavedMessage> { exportMessage }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportContainer, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            StatusMessage = $"Exported message to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to export message: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyMessageBodyAsync()
    {
        if (FormattedMessageBody == null) return;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(FormattedMessageBody);
            }
        }
    }

    #region Bulk Operations

    [RelayCommand]
    private void ToggleMultiSelectMode()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode)
        {
            SelectedMessages.Clear();
        }
    }

    [RelayCommand]
    private void ToggleMessageSelection(MessageInfo message)
    {
        if (SelectedMessages.Contains(message))
        {
            SelectedMessages.Remove(message);
        }
        else
        {
            SelectedMessages.Add(message);
        }
        SelectionVersion++;
    }

    [RelayCommand]
    private void SelectAllMessages()
    {
        SelectedMessages.Clear();
        foreach (var msg in FilteredMessages)
        {
            SelectedMessages.Add(msg);
        }
        SelectionVersion++;
    }

    [RelayCommand]
    private void DeselectAllMessages()
    {
        SelectedMessages.Clear();
        SelectionVersion++;
    }

    [RelayCommand]
    private async Task BulkResendMessagesAsync()
    {
        if (!HasSelectedMessages) return;

        string? entityName = SelectedQueue?.Name ?? SelectedTopic?.Name;
        if (entityName == null) return;

        var count = SelectedMessagesCount;
        
        if (Preferences.ConfirmBeforePurge) // Reusing this preference for destructive operations
        {
            ShowConfirmation(
                "Confirm Bulk Resend",
                $"Are you sure you want to resend {count} message(s) to '{entityName}'?",
                "Resend",
                async () => await ExecuteBulkResendAsync(entityName)
            );
        }
        else
        {
            await ExecuteBulkResendAsync(entityName);
        }
    }

    private async Task ExecuteBulkResendAsync(string entityName)
    {
        IsLoading = true;
        var count = SelectedMessagesCount;
        StatusMessage = $"Resending {count} message(s)...";

        try
        {
            var messagesToResend = SelectedMessages.ToList();
            int sentCount;

            if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
            {
                sentCount = await _connectionStringService.ResendMessagesAsync(
                    ActiveConnection.ConnectionString, entityName, messagesToResend);
            }
            else if (SelectedNamespace != null)
            {
                sentCount = await _serviceBus.ResendMessagesAsync(
                    SelectedNamespace.Endpoint, entityName, messagesToResend);
            }
            else
            {
                return;
            }

            StatusMessage = $"Successfully resent {sentCount} of {count} message(s)";
            SelectedMessages.Clear();
            await LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resending messages: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task BulkDeleteMessagesAsync()
    {
        if (!HasSelectedMessages) return;

        string? entityName = null;
        string? subscription = null;

        if (SelectedQueue != null)
        {
            entityName = SelectedQueue.Name;
        }
        else if (SelectedSubscription != null)
        {
            entityName = SelectedSubscription.TopicName;
            subscription = SelectedSubscription.Name;
        }

        if (entityName == null) return;

        var count = SelectedMessagesCount;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;

        ShowConfirmation(
            "Confirm Bulk Delete",
            $"Are you sure you want to delete {count} message(s) from '{targetName}'? This action cannot be undone.",
            "Delete",
            async () => await ExecuteBulkDeleteAsync(entityName, subscription)
        );
    }

    private async Task ExecuteBulkDeleteAsync(string entityName, string? subscription)
    {
        IsLoading = true;
        var count = SelectedMessagesCount;
        StatusMessage = $"Deleting {count} message(s)...";

        try
        {
            var sequenceNumbers = SelectedMessages.Select(m => m.SequenceNumber).ToList();
            int deletedCount;

            if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
            {
                deletedCount = await _connectionStringService.DeleteMessagesAsync(
                    ActiveConnection.ConnectionString, entityName, subscription, sequenceNumbers);
            }
            else if (SelectedNamespace != null)
            {
                deletedCount = await _serviceBus.DeleteMessagesAsync(
                    SelectedNamespace.Endpoint, entityName, subscription, sequenceNumbers);
            }
            else
            {
                return;
            }

            StatusMessage = $"Successfully deleted {deletedCount} of {count} message(s)";
            SelectedMessages.Clear();
            await LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting messages: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResubmitDeadLetterMessagesAsync()
    {
        if (!HasSelectedMessages || !ShowDeadLetter) return;

        string? entityName = null;
        string? subscription = null;

        if (SelectedQueue != null)
        {
            entityName = SelectedQueue.Name;
        }
        else if (SelectedSubscription != null)
        {
            entityName = SelectedSubscription.TopicName;
            subscription = SelectedSubscription.Name;
        }

        if (entityName == null) return;

        var count = SelectedMessagesCount;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;

        ShowConfirmation(
            "Confirm Resubmit Dead Letters",
            $"Are you sure you want to resubmit {count} message(s) from the dead letter queue back to '{targetName}'?",
            "Resubmit",
            async () => await ExecuteResubmitDeadLettersAsync(entityName, subscription)
        );
    }

    private async Task ExecuteResubmitDeadLettersAsync(string entityName, string? subscription)
    {
        IsLoading = true;
        var count = SelectedMessagesCount;
        StatusMessage = $"Resubmitting {count} dead letter message(s)...";

        try
        {
            var messagesToResubmit = SelectedMessages.ToList();
            int resubmittedCount;

            if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
            {
                resubmittedCount = await _connectionStringService.ResubmitDeadLetterMessagesAsync(
                    ActiveConnection.ConnectionString, entityName, subscription, messagesToResubmit);
            }
            else if (SelectedNamespace != null)
            {
                resubmittedCount = await _serviceBus.ResubmitDeadLetterMessagesAsync(
                    SelectedNamespace.Endpoint, entityName, subscription, messagesToResubmit);
            }
            else
            {
                return;
            }

            StatusMessage = $"Successfully resubmitted {resubmittedCount} of {count} message(s)";
            SelectedMessages.Clear();
            await LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resubmitting messages: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    [RelayCommand]
    private void ToggleSortOrder()
    {
        SortDescending = !SortDescending;
        ApplySorting();
    }

    private void ApplySorting()
    {
        if (Messages.Count == 0) return;

        var sorted = SortDescending
            ? Messages.OrderByDescending(m => m.EnqueuedTime).ToList()
            : Messages.OrderBy(m => m.EnqueuedTime).ToList();

        Messages.Clear();
        foreach (var m in sorted)
            Messages.Add(m);
    }

    [RelayCommand]
    private void ToggleStatusPopup()
    {
        ShowStatusPopup = !ShowStatusPopup;
    }

    [RelayCommand]
    private void CloseStatusPopup()
    {
        ShowStatusPopup = false;
    }

    // Connection Library Commands
    [RelayCommand]
    private async Task OpenConnectionLibraryAsync()
    {
        ConnectionLibraryViewModel = new ConnectionLibraryViewModel(
            _connectionStorage,
            _connectionStringService,
            OnConnectionSelected,
            msg => StatusMessage = msg,
            RefreshFavoriteConnectionsAsync
        );
        await ConnectionLibraryViewModel.LoadConnectionsAsync();
        ShowConnectionLibrary = true;
    }

    private async Task RefreshFavoriteConnectionsAsync()
    {
        FavoriteConnections.Clear();
        var connections = await _connectionStorage.GetConnectionsAsync();
        foreach (var conn in connections.Where(c => c.IsFavorite).OrderByDescending(c => c.CreatedAt))
        {
            FavoriteConnections.Add(conn);
        }
        OnPropertyChanged(nameof(HasFavoriteConnections));
    }

    [RelayCommand]
    private void CloseConnectionLibrary()
    {
        ShowConnectionLibrary = false;
        ConnectionLibraryViewModel = null;
    }

    // Settings Commands
    [RelayCommand]
    private void OpenSettings()
    {
        SettingsViewModel = new SettingsViewModel(CloseSettings, this);
        ShowSettings = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        ShowSettings = false;
        SettingsViewModel = null;
    }

    private async void OnConnectionSelected(SavedConnection connection)
    {
        ShowConnectionLibrary = false;
        ConnectionLibraryViewModel = null;
        await ConnectToSavedConnectionAsync(connection);
    }

    [RelayCommand]
    private async Task ConnectToSavedConnectionAsync(SavedConnection connection)
    {
        IsLoading = true;
        StatusMessage = $"Connecting to {connection.Name}...";

        try
        {
            // Clear any previous state
            Queues.Clear();
            Topics.Clear();
            TopicSubscriptions.Clear();
            Messages.Clear();
            Subscriptions.Clear();
            Namespaces.Clear();
            SelectedNamespace = null;
            SelectedQueue = null;
            SelectedTopic = null;
            SelectedSubscription = null;
            SelectedMessage = null;

            CurrentMode = ConnectionMode.ConnectionString;
            ActiveConnection = connection;

            // Load entity info based on connection type
            if (connection.Type == ConnectionType.Namespace)
            {
                // Namespace-level connection - load all queues and topics
                StatusMessage = "Loading queues and topics...";

                var queues = await _connectionStringService.GetQueuesFromConnectionAsync(connection.ConnectionString);
                foreach (var queue in queues)
                {
                    Queues.Add(queue);
                }

                var topics = await _connectionStringService.GetTopicsFromConnectionAsync(connection.ConnectionString);
                foreach (var topic in topics)
                {
                    Topics.Add(topic);
                }

                StatusMessage = $"Connected to {connection.Name} - {Queues.Count} queue(s), {Topics.Count} topic(s)";
            }
            else if (connection.Type == ConnectionType.Queue)
            {
                var queueInfo = await _connectionStringService.GetQueueInfoAsync(
                    connection.ConnectionString, connection.EntityName!);

                if (queueInfo != null)
                {
                    Queues.Add(queueInfo);
                    SelectedQueue = queueInfo;
                    SelectedEntity = queueInfo;
                    await LoadMessagesForConnectionAsync();
                }
                else
                {
                    StatusMessage = $"Could not find queue '{connection.EntityName}'";
                }
            }
            else if (connection.Type == ConnectionType.Topic)
            {
                var topicInfo = await _connectionStringService.GetTopicInfoAsync(
                    connection.ConnectionString, connection.EntityName!);

                if (topicInfo != null)
                {
                    Topics.Add(topicInfo);
                    SelectedTopic = topicInfo;
                    SelectedEntity = topicInfo;

                    // Load subscriptions for the topic
                    var subs = await _connectionStringService.GetTopicSubscriptionsAsync(
                        connection.ConnectionString, connection.EntityName!);
                    foreach (var sub in subs)
                    {
                        TopicSubscriptions.Add(sub);
                    }

                    StatusMessage = $"Connected to {connection.Name}";
                }
                else
                {
                    StatusMessage = $"Could not find topic '{connection.EntityName}'";
                }
            }

            if (connection.Type != ConnectionType.Namespace)
            {
                StatusMessage = $"Connected to {connection.Name}";
            }

            // Evaluate alerts after loading queues and subscriptions
            await _alertService.EvaluateAlertsAsync(Queues, TopicSubscriptions);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            CurrentMode = ConnectionMode.None;
            ActiveConnection = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectConnectionAsync()
    {
        CurrentMode = ConnectionMode.None;
        ActiveConnection = null;
        Queues.Clear();
        Topics.Clear();
        TopicSubscriptions.Clear();
        Messages.Clear();
        SelectedMessages.Clear();
        SelectedQueue = null;
        SelectedTopic = null;
        SelectedSubscription = null;
        SelectedMessage = null;
        SelectedEntity = null;
        IsMultiSelectMode = false;
        await LoadSavedConnectionsAsync();
        StatusMessage = "Disconnected";
    }

    private async Task LoadMessagesForConnectionAsync()
    {
        if (ActiveConnection == null) return;

        string? entityName = null;
        string? subscription = null;
        bool requiresSession = false;

        if (SelectedQueue != null)
        {
            entityName = SelectedQueue.Name;
            requiresSession = SelectedQueue.RequiresSession;
        }
        else if (SelectedSubscription != null)
        {
            entityName = SelectedSubscription.TopicName;
            subscription = SelectedSubscription.Name;
            requiresSession = SelectedSubscription.RequiresSession;
        }

        if (entityName == null) return;

        IsLoadingMessages = true;
        StatusMessage = "Loading messages...";
        MessageSearchText = ""; // Clear search when loading new messages
        SelectedMessages.Clear(); // Clear multi-select when loading new messages

        try
        {
            Messages.Clear();
            var msgs = await _connectionStringService.PeekMessagesAsync(
                ActiveConnection.ConnectionString, entityName, subscription, Preferences.DefaultMessageCount, ShowDeadLetter, requiresSession
            );

            // Apply sorting
            var sortedMsgs = SortDescending
                ? msgs.OrderByDescending(m => m.EnqueuedTime)
                : msgs.OrderBy(m => m.EnqueuedTime);

            foreach (var m in sortedMsgs)
                Messages.Add(m);

            ApplyMessageFilter(); // Apply filter after loading
            StatusMessage = $"{Messages.Count} message(s)";
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            StatusMessage = $"Error: {sbEx.Reason} - {sbEx.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingMessages = false;
        }
    }

    [RelayCommand]
    private async Task SelectSubscriptionForConnectionAsync(SubscriptionInfo sub)
    {
        if (CurrentMode != ConnectionMode.ConnectionString) return;

        SelectedSubscription = sub;
        SelectedQueue = null;
        SelectedEntity = sub;
        await LoadMessagesForConnectionAsync();
    }

    [RelayCommand]
    private async Task SelectQueueForConnectionAsync(QueueInfo queue)
    {
        if (CurrentMode != ConnectionMode.ConnectionString || ActiveConnection == null) return;

        SelectedQueue = queue;
        SelectedTopic = null;
        SelectedSubscription = null;
        SelectedEntity = queue;
        TopicSubscriptions.Clear();
        await LoadMessagesForConnectionAsync();
    }

    [RelayCommand]
    private async Task SelectTopicForConnectionAsync(TopicInfo topic)
    {
        if (CurrentMode != ConnectionMode.ConnectionString || ActiveConnection == null) return;

        SelectedTopic = topic;
        SelectedQueue = null;
        SelectedSubscription = null;
        SelectedEntity = topic;
        Messages.Clear();
        TopicSubscriptions.Clear();

        IsLoading = true;
        StatusMessage = $"Loading subscriptions for {topic.Name}...";

        try
        {
            var subs = await _connectionStringService.GetTopicSubscriptionsAsync(
                ActiveConnection.ConnectionString, topic.Name);
            foreach (var sub in subs)
            {
                TopicSubscriptions.Add(sub);
            }

            StatusMessage = $"{TopicSubscriptions.Count} subscription(s)";
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
    private async Task RefreshConnectionAsync()
    {
        if (ActiveConnection != null)
        {
            await ConnectToSavedConnectionAsync(ActiveConnection);
        }
    }

    [RelayCommand]
    private async Task LoadTopicSubscriptionsAsync(TopicInfo topic)
    {
        if (topic == null || topic.SubscriptionsLoaded || topic.IsLoadingSubscriptions) return;

        topic.IsLoadingSubscriptions = true;

        try
        {
            IEnumerable<SubscriptionInfo> subs;

            if (CurrentMode == ConnectionMode.ConnectionString && ActiveConnection != null)
            {
                subs = await _connectionStringService.GetTopicSubscriptionsAsync(
                    ActiveConnection.ConnectionString, topic.Name);
            }
            else if (CurrentMode == ConnectionMode.AzureAccount && SelectedNamespace != null)
            {
                subs = await _serviceBus.GetSubscriptionsAsync(SelectedNamespace.Id, topic.Name);
            }
            else
            {
                return;
            }

            topic.Subscriptions.Clear();
            foreach (var sub in subs)
            {
                topic.Subscriptions.Add(sub);
            }
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

    #region Confirmation Dialog

    private void ShowConfirmation(string title, string message, string confirmText, Func<Task> action)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogConfirmText = confirmText;
        _confirmDialogAction = action;
        ShowConfirmDialog = true;
    }

    [RelayCommand]
    private async Task ExecuteConfirmDialogAsync()
    {
        ShowConfirmDialog = false;
        if (_confirmDialogAction != null)
        {
            await _confirmDialogAction();
            _confirmDialogAction = null;
        }
    }

    [RelayCommand]
    private void CancelConfirmDialog()
    {
        ShowConfirmDialog = false;
        _confirmDialogAction = null;
    }

    #endregion
}
