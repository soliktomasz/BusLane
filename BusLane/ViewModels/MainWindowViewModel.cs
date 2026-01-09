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
public partial class MainWindowViewModel : ViewModelBase
{
    // Services (injected)
    private readonly IServiceBusService _serviceBus;
    private readonly IConnectionStringService _connectionStringService;
    private readonly IVersionService _versionService;
    private readonly IAlertService _alertService;
    private readonly IPreferencesService _preferencesService;
    private readonly IKeyboardShortcutService _keyboardShortcutService;
    private IFileDialogService? _fileDialogService;

    // Composed components
    public NavigationState Navigation { get; }
    public MessageOperationsViewModel MessageOps { get; }
    public ConnectionViewModel Connection { get; }
    public FeaturePanelsViewModel FeaturePanels { get; }

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

    // Confirmation dialog
    [ObservableProperty] private bool _showConfirmDialog;
    [ObservableProperty] private string _confirmDialogTitle = "";
    [ObservableProperty] private string _confirmDialogMessage = "";
    [ObservableProperty] private string _confirmDialogConfirmText = "Confirm";
    private Func<Task>? _confirmDialogAction;

    // Keyboard shortcuts dialog
    [ObservableProperty] private bool _showKeyboardShortcuts;

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

    /// <summary>
    /// Executes a service operation using either connection string or Azure account mode.
    /// Eliminates duplicated if/else blocks throughout the codebase.
    /// </summary>
    private async Task<T?> ExecuteServiceOperationAsync<T>(
        Func<IConnectionStringService, string, Task<T>> connectionStringOp,
        Func<IServiceBusService, string, Task<T>> azureOp)
    {
        if (Connection.CurrentMode == ConnectionMode.ConnectionString && Connection.ActiveConnection != null)
        {
            return await connectionStringOp(_connectionStringService, Connection.ActiveConnection.ConnectionString);
        }
        if (Navigation.SelectedNamespace != null)
        {
            return await azureOp(_serviceBus, Navigation.SelectedNamespace.Endpoint);
        }
        return default;
    }

    /// <summary>
    /// Executes a service operation without return value.
    /// </summary>
    private async Task ExecuteServiceOperationAsync(
        Func<IConnectionStringService, string, Task> connectionStringOp,
        Func<IServiceBusService, string, Task> azureOp)
    {
        if (Connection.CurrentMode == ConnectionMode.ConnectionString && Connection.ActiveConnection != null)
        {
            await connectionStringOp(_connectionStringService, Connection.ActiveConnection.ConnectionString);
        }
        else if (Navigation.SelectedNamespace != null)
        {
            await azureOp(_serviceBus, Navigation.SelectedNamespace.Endpoint);
        }
    }

    public MainWindowViewModel(
        IAzureAuthService auth,
        IServiceBusService serviceBus,
        IConnectionStorageService connectionStorage,
        IConnectionStringService connectionStringService,
        IVersionService versionService,
        IPreferencesService preferencesService,
        ILiveStreamService liveStreamService,
        IMetricsService metricsService,
        IAlertService alertService,
        INotificationService notificationService,
        IKeyboardShortcutService keyboardShortcutService,
        IFileDialogService? fileDialogService = null)
    {
        _serviceBus = serviceBus;
        _connectionStringService = connectionStringService;
        _versionService = versionService;
        _alertService = alertService;
        _preferencesService = preferencesService;
        _keyboardShortcutService = keyboardShortcutService;
        _fileDialogService = fileDialogService;

        // Initialize composed components
        Navigation = new NavigationState();
        
        Connection = new ConnectionViewModel(
            auth, serviceBus, connectionStorage, connectionStringService,
            msg => StatusMessage = msg,
            OnConnectedAsync,
            OnDisconnectedAsync);

        MessageOps = new MessageOperationsViewModel(
            serviceBus, connectionStringService, preferencesService,
            () => Navigation.CurrentEndpoint ?? Connection.CurrentEndpoint,
            () => Connection.CurrentConnectionString,
            () => Navigation.CurrentEntityName,
            () => Navigation.CurrentSubscriptionName,
            () => Navigation.CurrentEntityRequiresSession,
            () => Navigation.ShowDeadLetter,
            msg => StatusMessage = msg);

        FeaturePanels = new FeaturePanelsViewModel(
            liveStreamService, metricsService, alertService, notificationService,
            () => Navigation.CurrentEndpoint ?? Connection.CurrentEndpoint,
            () => Navigation.Queues,
            () => Navigation.Topics,
            () => Navigation.TopicSubscriptions,
            () => Navigation.SelectedQueue,
            () => Navigation.SelectedSubscription,
            msg => StatusMessage = msg);

        // Wire up property change handlers for cross-component dependencies
        Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Navigation.SelectedAzureSubscription))
                _ = LoadNamespacesAsync(Navigation.SelectedAzureSubscription?.Id);
            else if (e.PropertyName == nameof(Navigation.ShowDeadLetter))
                _ = MessageOps.LoadMessagesAsync();
        };

        InitializeAutoRefreshTimer();
    }


    public void SetFileDialogService(IFileDialogService fileDialogService) => _fileDialogService = fileDialogService;

    private async Task OnConnectedAsync()
    {
        if (Connection.CurrentMode == ConnectionMode.AzureAccount)
        {
            await LoadSubscriptionsAsync();
        }
        else if (Connection.ActiveConnection != null)
        {
            await LoadConnectionEntitiesAsync(Connection.ActiveConnection);
        }
    }

    private Task OnDisconnectedAsync()
    {
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
            if (_preferencesService.AutoRefreshMessages && Navigation.CurrentEntityName != null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await MessageOps.LoadMessagesAsync();
                });
            }

            if (Navigation.Queues.Count > 0 || Navigation.TopicSubscriptions.Count > 0)
            {
                await _alertService.EvaluateAlertsAsync(Navigation.Queues, Navigation.TopicSubscriptions);
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
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSubscriptionsAsync()
    {
        Navigation.Subscriptions.Clear();
        foreach (var sub in await _serviceBus.GetSubscriptionsAsync())
            Navigation.Subscriptions.Add(sub);

        if (Navigation.Subscriptions.Count > 0)
            Navigation.SelectedAzureSubscription = Navigation.Subscriptions[0];
    }

    private async Task LoadNamespacesAsync(string? subscriptionId)
    {
        if (subscriptionId == null) return;

        IsLoading = true;
        StatusMessage = "Loading namespaces...";

        try
        {
            Navigation.Namespaces.Clear();
            foreach (var ns in await _serviceBus.GetNamespacesAsync(subscriptionId))
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
        Navigation.SelectedNamespace = ns;
        IsLoading = true;
        StatusMessage = $"Loading {ns.Name}...";

        try
        {
            Navigation.ClearEntities();
            MessageOps.Clear();

            foreach (var q in await _serviceBus.GetQueuesAsync(ns.Id))
                Navigation.Queues.Add(q);

            foreach (var t in await _serviceBus.GetTopicsAsync(ns.Id))
                Navigation.Topics.Add(t);

            StatusMessage = $"{Navigation.Queues.Count} queue(s), {Navigation.Topics.Count} topic(s)";
            await _alertService.EvaluateAlertsAsync(Navigation.Queues, Navigation.TopicSubscriptions);
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
        Navigation.SelectedQueue = queue;
        Navigation.SelectedTopic = null;
        Navigation.SelectedSubscription = null;
        Navigation.SelectedEntity = queue;
        Navigation.TopicSubscriptions.Clear();
        await MessageOps.LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task SelectTopicAsync(TopicInfo topic)
    {
        if (Navigation.SelectedNamespace == null && Connection.ActiveConnection == null) return;

        Navigation.SelectedTopic = topic;
        Navigation.SelectedQueue = null;
        Navigation.SelectedSubscription = null;
        Navigation.SelectedEntity = topic;
        MessageOps.Clear();
        Navigation.TopicSubscriptions.Clear();

        IsLoading = true;
        StatusMessage = $"Loading subscriptions for {topic.Name}...";

        try
        {
            IEnumerable<SubscriptionInfo> subs;
            if (Connection.CurrentMode == ConnectionMode.ConnectionString && Connection.ActiveConnection != null)
            {
                subs = await _connectionStringService.GetTopicSubscriptionsAsync(
                    Connection.ActiveConnection.ConnectionString, topic.Name);
            }
            else
            {
                subs = await _serviceBus.GetSubscriptionsAsync(Navigation.SelectedNamespace!.Id, topic.Name);
            }

            foreach (var sub in subs)
                Navigation.TopicSubscriptions.Add(sub);

            StatusMessage = $"{Navigation.TopicSubscriptions.Count} subscription(s)";
            await _alertService.EvaluateAlertsAsync(Navigation.Queues, Navigation.TopicSubscriptions);
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
        Navigation.SelectedSubscription = sub;
        Navigation.SelectedQueue = null;
        Navigation.SelectedEntity = sub;
        await MessageOps.LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task LoadTopicSubscriptionsAsync(TopicInfo topic)
    {
        if (topic.SubscriptionsLoaded || topic.IsLoadingSubscriptions) return;

        topic.IsLoadingSubscriptions = true;

        try
        {
            IEnumerable<SubscriptionInfo> subs;

            if (Connection.CurrentMode == ConnectionMode.ConnectionString && Connection.ActiveConnection != null)
            {
                subs = await _connectionStringService.GetTopicSubscriptionsAsync(
                    Connection.ActiveConnection.ConnectionString, topic.Name);
            }
            else if (Connection.CurrentMode == ConnectionMode.AzureAccount && Navigation.SelectedNamespace != null)
            {
                subs = await _serviceBus.GetSubscriptionsAsync(Navigation.SelectedNamespace.Id, topic.Name);
            }
            else
            {
                return;
            }

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

    #region Connection Mode Entity Loading

    private async Task LoadConnectionEntitiesAsync(SavedConnection connection)
    {
        IsLoading = true;

        try
        {
            Navigation.Clear();

            if (connection.Type == ConnectionType.Namespace)
            {
                StatusMessage = "Loading queues and topics...";

                var queues = await _connectionStringService.GetQueuesFromConnectionAsync(connection.ConnectionString);
                foreach (var queue in queues)
                    Navigation.Queues.Add(queue);

                var topics = await _connectionStringService.GetTopicsFromConnectionAsync(connection.ConnectionString);
                foreach (var topic in topics)
                    Navigation.Topics.Add(topic);

                StatusMessage = $"Connected - {Navigation.Queues.Count} queue(s), {Navigation.Topics.Count} topic(s)";
            }
            else if (connection.Type == ConnectionType.Queue)
            {
                var queueInfo = await _connectionStringService.GetQueueInfoAsync(
                    connection.ConnectionString, connection.EntityName!);

                if (queueInfo != null)
                {
                    Navigation.Queues.Add(queueInfo);
                    Navigation.SelectedQueue = queueInfo;
                    Navigation.SelectedEntity = queueInfo;
                    await MessageOps.LoadMessagesAsync();
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
                    Navigation.Topics.Add(topicInfo);
                    Navigation.SelectedTopic = topicInfo;
                    Navigation.SelectedEntity = topicInfo;

                    var subs = await _connectionStringService.GetTopicSubscriptionsAsync(
                        connection.ConnectionString, connection.EntityName!);
                    foreach (var sub in subs)
                        Navigation.TopicSubscriptions.Add(sub);
                }
                else
                {
                    StatusMessage = $"Could not find topic '{connection.EntityName}'";
                }
            }

            await _alertService.EvaluateAlertsAsync(Navigation.Queues, Navigation.TopicSubscriptions);
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

    #region Message Operations (delegated to MessageOps with some coordination)

    [RelayCommand]
    private async Task LoadMessagesAsync() => await MessageOps.LoadMessagesAsync();

    [RelayCommand]
    private void SelectMessage(MessageInfo message) => MessageOps.SelectMessage(message);

    [RelayCommand]
    private void ClearSelectedMessage() => MessageOps.ClearSelectedMessage();

    [RelayCommand]
    private void ToggleMultiSelectMode() => MessageOps.ToggleMultiSelectMode();

    [RelayCommand]
    private void ToggleMessageSelection(MessageInfo message) => MessageOps.ToggleMessageSelection(message);

    [RelayCommand]
    private void SelectAllMessages() => MessageOps.SelectAllMessages();

    [RelayCommand]
    private void DeselectAllMessages() => MessageOps.DeselectAllMessages();

    [RelayCommand]
    private void ToggleSortOrder() => MessageOps.ToggleSortOrder();

    [RelayCommand]
    private void ClearMessageSearch() => MessageOps.ClearMessageSearch();

    [RelayCommand]
    private async Task CopyMessageBodyAsync() => await MessageOps.CopyMessageBodyAsync();

    #endregion

    #region Send Message

    [RelayCommand]
    private void OpenSendMessagePopup()
    {
        var entityName = Navigation.CurrentEntityName;
        if (entityName == null) return;

        if (Connection.CurrentMode == ConnectionMode.ConnectionString && Connection.ActiveConnection != null)
        {
            SendMessageViewModel = new SendMessageViewModel(
                _connectionStringService,
                Connection.ActiveConnection.ConnectionString,
                entityName,
                CloseSendMessagePopup,
                msg => StatusMessage = msg,
                _fileDialogService);
        }
        else if (Navigation.SelectedNamespace != null)
        {
            SendMessageViewModel = new SendMessageViewModel(
                _serviceBus,
                Navigation.SelectedNamespace.Endpoint,
                entityName,
                CloseSendMessagePopup,
                msg => StatusMessage = msg,
                _fileDialogService);
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
        await MessageOps.LoadMessagesAsync();
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
        var msg = message ?? MessageOps.SelectedMessage;
        if (msg == null) return;

        var entityName = Navigation.CurrentEntityName;
        if (entityName == null) return;

        IsLoading = true;
        StatusMessage = "Resending message...";

        try
        {
            var properties = msg.Properties.ToDictionary(p => p.Key, p => p.Value);

            await ExecuteServiceOperationAsync(
                async (svc, connStr) => await svc.SendMessageAsync(
                    connStr, entityName, msg.Body, properties,
                    msg.ContentType, msg.CorrelationId, null, msg.SessionId, msg.Subject,
                    msg.To, msg.ReplyTo, msg.ReplyToSessionId, msg.PartitionKey, msg.TimeToLive, null),
                async (svc, endpoint) => await svc.SendMessageAsync(
                    endpoint, entityName, msg.Body, properties,
                    msg.ContentType, msg.CorrelationId, null, msg.SessionId, msg.Subject,
                    msg.To, msg.ReplyTo, msg.ReplyToSessionId, msg.PartitionKey, msg.TimeToLive, null));

            StatusMessage = "Message resent successfully";
            await MessageOps.LoadMessagesAsync();
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
        var msg = message ?? MessageOps.SelectedMessage;
        if (msg == null) return;

        var entityName = Navigation.CurrentEntityName;
        if (entityName == null) return;

        if (Connection.CurrentMode == ConnectionMode.ConnectionString && Connection.ActiveConnection != null)
        {
            SendMessageViewModel = new SendMessageViewModel(
                _connectionStringService,
                Connection.ActiveConnection.ConnectionString,
                entityName,
                CloseSendMessagePopup,
                status => StatusMessage = status,
                _fileDialogService);
        }
        else if (Navigation.SelectedNamespace != null)
        {
            SendMessageViewModel = new SendMessageViewModel(
                _serviceBus,
                Navigation.SelectedNamespace.Endpoint,
                entityName,
                CloseSendMessagePopup,
                status => StatusMessage = status,
                _fileDialogService);
        }
        else
        {
            return;
        }

        SendMessageViewModel!.PopulateFromMessage(msg);
        ShowSendMessagePopup = true;
        MessageOps.ClearSelectedMessage();
    }

    #endregion

    #region Export

    private static readonly Avalonia.Platform.Storage.FilePickerFileType JsonFileType = new("JSON Files")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" }
    };

    [RelayCommand]
    private async Task ExportMessageAsync(MessageInfo? message = null)
    {
        var msg = message ?? MessageOps.SelectedMessage;
        if (msg == null || _fileDialogService == null)
        {
            StatusMessage = _fileDialogService == null ? "File dialog service not available" : "No message selected";
            return;
        }

        try
        {
            var safeName = string.Join("_", (msg.MessageId ?? "message").Split(Path.GetInvalidFileNameChars()));
            var defaultFileName = $"Message_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await _fileDialogService.SaveFileAsync("Export Message", defaultFileName, new[] { JsonFileType });

            if (string.IsNullOrEmpty(filePath)) return;

            var exportMessage = new SavedMessage
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
                CustomProperties = msg.Properties?.ToDictionary(p => p.Key, p => p.Value?.ToString() ?? "") ?? new Dictionary<string, string>(),
                CreatedAt = DateTime.UtcNow
            };

            var exportContainer = new MessageExportContainer
            {
                Description = $"Exported message from {Navigation.SelectedQueue?.Name ?? Navigation.SelectedSubscription?.Name}",
                Messages = new List<SavedMessage> { exportMessage }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportContainer, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            StatusMessage = $"Exported message to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to export message: {ex.Message}";
        }
    }

    #endregion

    #region Purge & Bulk Operations

    [RelayCommand]
    private async Task PurgeMessagesAsync()
    {
        var entityName = Navigation.CurrentEntityName;
        if (entityName == null) return;

        var subscription = Navigation.CurrentSubscriptionName;

        if (_preferencesService.ConfirmBeforePurge)
        {
            var queueType = Navigation.ShowDeadLetter ? "dead letter queue" : "queue";
            var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;
            ShowConfirmation(
                "Confirm Purge",
                $"Are you sure you want to purge all messages from the {queueType} of '{targetName}'? This action cannot be undone.",
                "Purge",
                async () => await ExecutePurgeAsync(entityName, subscription));
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
            await ExecuteServiceOperationAsync(
                async (svc, connStr) => await svc.PurgeMessagesAsync(connStr, entityName, subscription, Navigation.ShowDeadLetter),
                async (svc, endpoint) => await svc.PurgeMessagesAsync(endpoint, entityName, subscription, Navigation.ShowDeadLetter));

            StatusMessage = "Purge complete";
            await MessageOps.LoadMessagesAsync();
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
    private async Task BulkResendMessagesAsync()
    {
        if (!MessageOps.HasSelectedMessages) return;

        var entityName = Navigation.CurrentEntityName;
        if (entityName == null) return;

        var count = MessageOps.SelectedMessagesCount;

        if (_preferencesService.ConfirmBeforePurge)
        {
            ShowConfirmation(
                "Confirm Bulk Resend",
                $"Are you sure you want to resend {count} message(s) to '{entityName}'?",
                "Resend",
                async () => await ExecuteBulkResendAsync(entityName));
        }
        else
        {
            await ExecuteBulkResendAsync(entityName);
        }
    }

    private async Task ExecuteBulkResendAsync(string entityName)
    {
        IsLoading = true;
        var count = MessageOps.SelectedMessagesCount;
        StatusMessage = $"Resending {count} message(s)...";

        try
        {
            var messagesToResend = MessageOps.SelectedMessages.ToList();

            var sentCount = await ExecuteServiceOperationAsync(
                async (svc, connStr) => await svc.ResendMessagesAsync(connStr, entityName, messagesToResend),
                async (svc, endpoint) => await svc.ResendMessagesAsync(endpoint, entityName, messagesToResend));

            StatusMessage = $"Successfully resent {sentCount} of {count} message(s)";
            MessageOps.SelectedMessages.Clear();
            await MessageOps.LoadMessagesAsync();
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
    private void BulkDeleteMessagesAsync()
    {
        if (!MessageOps.HasSelectedMessages) return;

        var entityName = Navigation.CurrentEntityName;
        if (entityName == null) return;

        var subscription = Navigation.CurrentSubscriptionName;
        var count = MessageOps.SelectedMessagesCount;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;

        ShowConfirmation(
            "Confirm Bulk Delete",
            $"Are you sure you want to delete {count} message(s) from '{targetName}'? This action cannot be undone.",
            "Delete",
            async () => await ExecuteBulkDeleteAsync(entityName, subscription));
    }

    private async Task ExecuteBulkDeleteAsync(string entityName, string? subscription)
    {
        IsLoading = true;
        var count = MessageOps.SelectedMessagesCount;
        StatusMessage = $"Deleting {count} message(s)...";

        try
        {
            var sequenceNumbers = MessageOps.SelectedMessages.Select(m => m.SequenceNumber).ToList();

            var deletedCount = await ExecuteServiceOperationAsync(
                async (svc, connStr) => await svc.DeleteMessagesAsync(connStr, entityName, subscription, sequenceNumbers, Navigation.ShowDeadLetter),
                async (svc, endpoint) => await svc.DeleteMessagesAsync(endpoint, entityName, subscription, sequenceNumbers, Navigation.ShowDeadLetter));

            StatusMessage = $"Successfully deleted {deletedCount} of {count} message(s)";
            MessageOps.SelectedMessages.Clear();
            await MessageOps.LoadMessagesAsync();
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
    private void ResubmitDeadLetterMessagesAsync()
    {
        if (!MessageOps.HasSelectedMessages || !Navigation.ShowDeadLetter) return;

        var entityName = Navigation.CurrentEntityName;
        if (entityName == null) return;

        var subscription = Navigation.CurrentSubscriptionName;
        var count = MessageOps.SelectedMessagesCount;
        var targetName = subscription != null ? $"{entityName}/{subscription}" : entityName;

        ShowConfirmation(
            "Confirm Resubmit Dead Letters",
            $"Are you sure you want to resubmit {count} message(s) from the dead letter queue back to '{targetName}'?",
            "Resubmit",
            async () => await ExecuteResubmitDeadLettersAsync(entityName, subscription));
    }

    private async Task ExecuteResubmitDeadLettersAsync(string entityName, string? subscription)
    {
        IsLoading = true;
        var count = MessageOps.SelectedMessagesCount;
        StatusMessage = $"Resubmitting {count} dead letter message(s)...";

        try
        {
            var messagesToResubmit = MessageOps.SelectedMessages.ToList();

            var resubmittedCount = await ExecuteServiceOperationAsync(
                async (svc, connStr) => await svc.ResubmitDeadLetterMessagesAsync(connStr, entityName, subscription, messagesToResubmit),
                async (svc, endpoint) => await svc.ResubmitDeadLetterMessagesAsync(endpoint, entityName, subscription, messagesToResubmit));

            StatusMessage = $"Successfully resubmitted {resubmittedCount} of {count} message(s)";
            MessageOps.SelectedMessages.Clear();
            await MessageOps.LoadMessagesAsync();
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

    #region Refresh

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (Connection.CurrentMode == ConnectionMode.ConnectionString && Connection.ActiveConnection != null)
        {
            await LoadConnectionEntitiesAsync(Connection.ActiveConnection);
        }
        else if (Navigation.SelectedNamespace != null)
        {
            await SelectNamespaceAsync(Navigation.SelectedNamespace);
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

    /// <summary>
    /// Toggles the dead letter view for the current entity.
    /// </summary>
    [RelayCommand]
    private async Task ToggleDeadLetterViewAsync()
    {
        Navigation.ShowDeadLetter = !Navigation.ShowDeadLetter;
        await MessageOps.LoadMessagesAsync();
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
    private async Task OpenConnectionLibraryAsync() => await Connection.OpenConnectionLibraryAsync();

    [RelayCommand]
    private void CloseConnectionLibrary() => Connection.CloseConnectionLibrary();

    [RelayCommand]
    private async Task ConnectToSavedConnectionAsync(SavedConnection connection)
    {
        IsLoading = true;
        try
        {
            await Connection.ConnectToSavedConnectionAsync(connection);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectConnectionAsync() => await Connection.DisconnectConnectionAsync();

    [RelayCommand]
    private async Task RefreshConnectionAsync()
    {
        if (Connection.ActiveConnection != null)
            await LoadConnectionEntitiesAsync(Connection.ActiveConnection);
    }

    // Aliases for connection-string mode navigation
    [RelayCommand]
    private async Task SelectQueueForConnectionAsync(QueueInfo queue) => await SelectQueueAsync(queue);

    [RelayCommand]
    private async Task SelectTopicForConnectionAsync(TopicInfo topic) => await SelectTopicAsync(topic);

    [RelayCommand]
    private async Task SelectSubscriptionForConnectionAsync(SubscriptionInfo sub) => await SelectSubscriptionAsync(sub);

    #endregion

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
