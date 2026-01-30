// BusLane/ViewModels/Core/ConnectionTabViewModel.cs
using Azure.Core;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            _operations = null;
            SavedConnection = null;
            Mode = ConnectionMode.None;
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

        try
        {
            Namespace = ns;
            _operations = operationsFactory.CreateFromAzureCredential(ns.Endpoint, ns.Id, credential);
            Mode = ConnectionMode.AzureAccount;

            TabTitle = ns.Name;
            TabSubtitle = ns.Endpoint;

            await LoadNamespaceEntitiesAsync();

            IsConnected = true;
            StatusMessage = "Connected";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            _operations = null;
            Namespace = null;
            Mode = ConnectionMode.None;
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
        _operations = null;
        SavedConnection = null;
        Namespace = null;
        Mode = ConnectionMode.None;
        IsConnected = false;

        Navigation.Clear();
        MessageOps.Clear();

        StatusMessage = "Disconnected";
        return Task.CompletedTask;
    }

    private async Task LoadEntitiesAsync(SavedConnection connection)
    {
        if (_operations == null) return;

        Navigation.Clear();

        if (connection.Type == ConnectionType.Namespace)
        {
            await LoadNamespaceEntitiesAsync();
        }
        else if (connection.Type == ConnectionType.Queue && connection.EntityName != null)
        {
            var queueInfo = await _operations.GetQueueInfoAsync(connection.EntityName);
            if (queueInfo != null)
            {
                Navigation.Queues.Add(queueInfo);
                Navigation.SelectedQueue = queueInfo;
                Navigation.SelectedEntity = queueInfo;
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
            }
        }
    }

    private async Task LoadNamespaceEntitiesAsync()
    {
        if (_operations == null) return;

        var queues = await _operations.GetQueuesAsync();
        foreach (var queue in queues)
            Navigation.Queues.Add(queue);

        var topics = await _operations.GetTopicsAsync();
        foreach (var topic in topics)
            Navigation.Topics.Add(topic);

        StatusMessage = $"{Navigation.Queues.Count} queue(s), {Navigation.Topics.Count} topic(s)";
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
        public bool ShowDeadLetterBadges { get; set; } = true;
        public bool EnableMessagePreview { get; set; } = true;
        public bool ShowNavigationPanel { get; set; } = true;
        public string Theme { get; set; } = "System";
        public int LiveStreamPollingIntervalSeconds { get; set; } = 1;
        public bool RestoreTabsOnStartup { get; set; } = true;
        public string OpenTabsJson { get; set; } = "[]";
        public bool AutoCheckForUpdates { get; set; } = true;
        public string? SkippedUpdateVersion { get; set; }
        public DateTime? UpdateRemindLaterDate { get; set; }
        public event EventHandler? PreferencesChanged;
        public void Save() { }
        public void Load() { }
    }
#pragma warning restore CS0067
}
