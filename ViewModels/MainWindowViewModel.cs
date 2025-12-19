using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

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

    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _selectedSubscriptionId;
    [ObservableProperty] private AzureSubscription? _selectedAzureSubscription;
    [ObservableProperty] private ServiceBusNamespace? _selectedNamespace;
    [ObservableProperty] private object? _selectedEntity;
    [ObservableProperty] private QueueInfo? _selectedQueue;
    [ObservableProperty] private TopicInfo? _selectedTopic;
    [ObservableProperty] private SubscriptionInfo? _selectedSubscription;
    [ObservableProperty] private MessageInfo? _selectedMessage;
    [ObservableProperty] private bool _showDeadLetter;
    [ObservableProperty] private string _newMessageBody = "";
    [ObservableProperty] private bool _showStatusPopup;
    [ObservableProperty] private bool _showSendMessagePopup;
    [ObservableProperty] private SendMessageViewModel? _sendMessageViewModel;
    
    // Connection mode properties
    [ObservableProperty] private ConnectionMode _currentMode = ConnectionMode.None;
    [ObservableProperty] private bool _showConnectionLibrary;
    [ObservableProperty] private ConnectionLibraryViewModel? _connectionLibraryViewModel;
    [ObservableProperty] private SavedConnection? _activeConnection;
    
    public ObservableCollection<AzureSubscription> Subscriptions { get; } = [];
    public ObservableCollection<ServiceBusNamespace> Namespaces { get; } = [];
    public ObservableCollection<QueueInfo> Queues { get; } = [];
    public ObservableCollection<TopicInfo> Topics { get; } = [];
    public ObservableCollection<SubscriptionInfo> TopicSubscriptions { get; } = [];
    public ObservableCollection<MessageInfo> Messages { get; } = [];
    public ObservableCollection<SavedConnection> SavedConnections { get; } = [];
    
    // Computed properties for visibility bindings (Count doesn't notify on collection changes)
    public bool HasQueues => Queues.Count > 0;
    public bool HasTopics => Topics.Count > 0;

    public MainWindowViewModel(
        IAzureAuthService auth, 
        IServiceBusService serviceBus,
        IConnectionStorageService connectionStorage,
        IConnectionStringService connectionStringService)
    {
        _auth = auth;
        _serviceBus = serviceBus;
        _connectionStorage = connectionStorage;
        _connectionStringService = connectionStringService;
        _auth.AuthenticationChanged += (_, authenticated) => IsAuthenticated = authenticated;
        
        // Subscribe to collection changes to notify visibility properties
        Queues.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasQueues));
        Topics.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTopics));
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading saved connections...";
        
        try
        {
            // Load saved connections first
            await LoadSavedConnectionsAsync();
            
            // Try to restore previous Azure session using cached credentials
            StatusMessage = "Checking for saved Azure credentials...";
            if (await _auth.TrySilentLoginAsync())
            {
                CurrentMode = ConnectionMode.AzureAccount;
                StatusMessage = "Loading subscriptions...";
                await LoadSubscriptionsAsync();
                StatusMessage = "Restored previous Azure session";
            }
            else
            {
                StatusMessage = SavedConnections.Count > 0 
                    ? "Select a saved connection or sign in with Azure"
                    : "Add a connection or sign in with Azure to get started";
            }
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
        var connections = await _connectionStorage.GetConnectionsAsync();
        foreach (var conn in connections.OrderByDescending(c => c.CreatedAt))
        {
            SavedConnections.Add(conn);
        }
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
        
        IsLoading = true;
        StatusMessage = "Loading messages...";
        
        try
        {
            Messages.Clear();
            var msgs = await _serviceBus.PeekMessagesAsync(
                SelectedNamespace.Endpoint, entityName, subscription, 100, ShowDeadLetter, requiresSession
            );
            
            foreach (var m in msgs)
                Messages.Add(m);
            
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
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleDeadLetterAsync()
    {
        ShowDeadLetter = !ShowDeadLetter;
        await LoadMessagesAsync();
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
                msg => StatusMessage = msg
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
                msg => StatusMessage = msg
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
            msg => StatusMessage = msg
        );
        await ConnectionLibraryViewModel.LoadConnectionsAsync();
        ShowConnectionLibrary = true;
    }

    [RelayCommand]
    private void CloseConnectionLibrary()
    {
        ShowConnectionLibrary = false;
        ConnectionLibraryViewModel = null;
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
        SelectedQueue = null;
        SelectedTopic = null;
        SelectedSubscription = null;
        SelectedMessage = null;
        SelectedEntity = null;
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

        IsLoading = true;
        StatusMessage = "Loading messages...";

        try
        {
            Messages.Clear();
            var msgs = await _connectionStringService.PeekMessagesAsync(
                ActiveConnection.ConnectionString, entityName, subscription, 100, ShowDeadLetter, requiresSession
            );

            foreach (var m in msgs)
                Messages.Add(m);

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
            IsLoading = false;
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
}

