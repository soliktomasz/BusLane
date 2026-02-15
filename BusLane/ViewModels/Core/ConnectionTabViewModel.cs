namespace BusLane.ViewModels.Core;

// BusLane/ViewModels/Core/ConnectionTabViewModel.cs
using Azure.Core;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Represents a single connection tab with its own navigation state and message operations.
/// Each tab encapsulates a complete connection to a Service Bus namespace.
/// </summary>
public partial class ConnectionTabViewModel : ViewModelBase
{
    private readonly IPreferencesService _preferencesService;
    private readonly ILogSink _logSink;

    // Identity
    [ObservableProperty] private string _tabId;
    [ObservableProperty] private string _tabTitle;
    [ObservableProperty] private string _tabSubtitle;
    [ObservableProperty] private ConnectionMode _mode = ConnectionMode.None;

    // State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string? _statusMessage;

    // Composed components
    public NavigationState Navigation { get; }
    public MessageOperationsViewModel MessageOps { get; }

    // Connection resources (set after connection)
    private IServiceBusOperations? _operations;

    [ObservableProperty]
    private SavedConnection? _savedConnection;

    [ObservableProperty]
    private ServiceBusNamespace? _namespace;

    public ConnectionTabViewModel(string tabId, string tabTitle, string tabSubtitle)
        : this(tabId, tabTitle, tabSubtitle, null!, null!)
    {
    }

    public ConnectionTabViewModel(
        string tabId,
        string tabTitle,
        string tabSubtitle,
        IPreferencesService preferencesService,
        ILogSink logSink)
    {
        _tabId = tabId;
        _tabTitle = tabTitle;
        _tabSubtitle = tabSubtitle;
        _preferencesService = preferencesService;
        _logSink = logSink;

        Navigation = new NavigationState();

        MessageOps = new MessageOperationsViewModel(
            () => _operations,
            preferencesService ?? new DummyPreferencesService(),
            logSink,
            () => Navigation.CurrentEntityName,
            () => Navigation.CurrentSubscriptionName,
            () => Navigation.CurrentEntityRequiresSession,
            () => Navigation.ShowDeadLetter,
            () => GetKnownMessageCount(),
            msg => StatusMessage = msg);
    }

    /// <summary>
    /// Gets the current operations instance for this tab.
    /// </summary>
    public IServiceBusOperations? Operations => _operations;


    /// <summary>
    /// Connects to a Service Bus namespace using a saved connection string.
    /// </summary>
    public async Task ConnectWithConnectionStringAsync(
        SavedConnection connection,
        IServiceBusOperationsFactory operationsFactory)
    {
        IsLoading = true;
        StatusMessage = $"Connecting to {connection.Name}...";
        LogActivity(LogLevel.Info, $"Tab '{TabTitle}': connecting with saved connection '{connection.Name}'");

        try
        {
            SavedConnection = connection;
            _operations = operationsFactory.CreateFromConnectionString(connection.ConnectionString);
            Mode = ConnectionMode.ConnectionString;

            TabTitle = connection.Name;
            TabSubtitle = connection.Endpoint ?? "";

            await LoadEntitiesAsync(connection);

            IsConnected = true;
            StatusMessage = "Connected";
            LogActivity(LogLevel.Info, $"Tab '{TabTitle}': connection established");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            _operations = null;
            SavedConnection = null;
            Mode = ConnectionMode.None;
            LogActivity(LogLevel.Error, $"Tab '{TabTitle}': connection failed", ex.Message);
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Connects to a Service Bus namespace using Azure credentials.
    /// </summary>
    public async Task ConnectWithAzureCredentialAsync(
        ServiceBusNamespace ns,
        TokenCredential credential,
        IServiceBusOperationsFactory operationsFactory)
    {
        IsLoading = true;
        StatusMessage = $"Connecting to {ns.Name}...";
        LogActivity(LogLevel.Info, $"Tab '{TabTitle}': connecting to namespace '{ns.Name}' with Azure credential");

        try
        {
            Namespace = ns;
            Navigation.SelectedNamespace = ns;
            _operations = operationsFactory.CreateFromAzureCredential(ns.Endpoint, ns.Id, credential);
            Mode = ConnectionMode.AzureAccount;

            TabTitle = ns.Name;
            TabSubtitle = ns.Endpoint;

            await LoadNamespaceEntitiesAsync();

            IsConnected = true;
            StatusMessage = "Connected";
            LogActivity(LogLevel.Info, $"Tab '{TabTitle}': Azure connection established");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            _operations = null;
            Namespace = null;
            Mode = ConnectionMode.None;
            LogActivity(LogLevel.Error, $"Tab '{TabTitle}': Azure connection failed", ex.Message);
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Disconnects from the current Service Bus namespace.
    /// </summary>
    public Task DisconnectAsync()
    {
        var tabTitle = TabTitle;

        _operations = null;
        SavedConnection = null;
        Namespace = null;
        Mode = ConnectionMode.None;
        IsConnected = false;

        Navigation.Clear();
        MessageOps.Clear();

        StatusMessage = "Disconnected";
        LogActivity(LogLevel.Info, $"Tab '{tabTitle}': disconnected");
        return Task.CompletedTask;
    }

    private async Task LoadEntitiesAsync(SavedConnection connection)
    {
        if (_operations == null) return;

        Navigation.Clear();

        if (connection.Type == ConnectionType.Namespace)
        {
            await LoadNamespaceEntitiesAsync();
            LogActivity(LogLevel.Info, $"Tab '{TabTitle}': loaded namespace entities");
        }
        else if (connection.Type == ConnectionType.Queue && connection.EntityName != null)
        {
            var queueInfo = await _operations.GetQueueInfoAsync(connection.EntityName);
            if (queueInfo != null)
            {
                Navigation.Queues.Add(queueInfo);
                Navigation.SelectedQueue = queueInfo;
                Navigation.SelectedEntity = queueInfo;
                LogActivity(LogLevel.Info, $"Tab '{TabTitle}': loaded queue '{queueInfo.Name}'");
            }
        }
        else if (connection.Type == ConnectionType.Topic && connection.EntityName != null)
        {
            var topicInfo = await _operations.GetTopicInfoAsync(connection.EntityName);
            if (topicInfo != null)
            {
                Navigation.Topics.Add(topicInfo);
                Navigation.SelectedTopic = topicInfo;
                Navigation.SelectedEntity = topicInfo;

                var subs = await _operations.GetSubscriptionsAsync(connection.EntityName);
                foreach (var sub in subs)
                    Navigation.TopicSubscriptions.Add(sub);

                LogActivity(LogLevel.Info, $"Tab '{TabTitle}': loaded topic '{topicInfo.Name}' with {Navigation.TopicSubscriptions.Count} subscription(s)");
            }
        }
    }

    private async Task LoadNamespaceEntitiesAsync()
    {
        if (_operations == null) return;

        // Keep navigation namespace state in sync with the connected tab namespace.
        Navigation.SelectedNamespace = Namespace;

        var queues = await _operations.GetQueuesAsync();
        foreach (var queue in queues)
            Navigation.Queues.Add(queue);

        var topics = await _operations.GetTopicsAsync();
        foreach (var topic in topics)
            Navigation.Topics.Add(topic);

        StatusMessage = $"{Navigation.Queues.Count} queue(s), {Navigation.Topics.Count} topic(s)";
        LogActivity(
            LogLevel.Info,
            $"Tab '{TabTitle}': discovered {Navigation.Queues.Count} queue(s) and {Navigation.Topics.Count} topic(s)");
    }

    /// <summary>
    /// Refreshes the namespace entities (queues and topics) for this tab.
    /// </summary>
    public async Task RefreshNamespaceEntitiesAsync()
    {
        if (_operations == null) return;

        IsLoading = true;
        StatusMessage = "Refreshing...";
        LogActivity(LogLevel.Info, $"Tab '{TabTitle}': refreshing namespace entities");

        try
        {
            Navigation.Queues.Clear();
            Navigation.Topics.Clear();
            Navigation.SelectedNamespace = Namespace;

            var queues = await _operations.GetQueuesAsync();
            foreach (var queue in queues)
                Navigation.Queues.Add(queue);

            var topics = await _operations.GetTopicsAsync();
            foreach (var topic in topics)
                Navigation.Topics.Add(topic);

            StatusMessage = $"{Navigation.Queues.Count} queue(s), {Navigation.Topics.Count} topic(s)";
            LogActivity(
                LogLevel.Info,
                $"Tab '{TabTitle}': refresh completed with {Navigation.Queues.Count} queue(s) and {Navigation.Topics.Count} topic(s)");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing: {ex.Message}";
            LogActivity(LogLevel.Error, $"Tab '{TabTitle}': refresh failed", ex.Message);
            throw;
        }
        finally
        {
            IsLoading = false;
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

    private void LogActivity(LogLevel level, string message, string? details = null)
    {
        _logSink?.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            level,
            message,
            details));
    }

    // Minimal implementation for parameterless constructor
    #pragma warning disable CS0067 // Event is never used (required by interface but not needed in dummy implementation)
    private class DummyPreferencesService : IPreferencesService
    {
        public bool ConfirmBeforeDelete { get; set; } = true;
        public bool ConfirmBeforePurge { get; set; } = true;
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; } = 30;
        public int DefaultMessageCount { get; set; } = 100;
        public int MessagesPerPage { get; set; } = 100;
        public int MaxTotalMessages { get; set; } = 500;
        public bool ShowDeadLetterBadges { get; set; } = true;
        public bool EnableMessagePreview { get; set; } = true;
        public bool ShowNavigationPanel { get; set; } = true;
        public string Theme { get; set; } = "System";
        public int LiveStreamPollingIntervalSeconds { get; set; } = 1;
        public bool RestoreTabsOnStartup { get; set; } = true;
        public string OpenTabsJson { get; set; } = "[]";
        public bool EnableTelemetry { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }
        public event EventHandler? PreferencesChanged;
        public void Save() { }
        public void Load() { }
    }
#pragma warning restore CS0067
}
