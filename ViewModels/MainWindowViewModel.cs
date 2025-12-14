using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAzureAuthService _auth;
    private readonly IServiceBusService _serviceBus;

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
    
    public ObservableCollection<AzureSubscription> Subscriptions { get; } = [];
    public ObservableCollection<ServiceBusNamespace> Namespaces { get; } = [];
    public ObservableCollection<QueueInfo> Queues { get; } = [];
    public ObservableCollection<TopicInfo> Topics { get; } = [];
    public ObservableCollection<SubscriptionInfo> TopicSubscriptions { get; } = [];
    public ObservableCollection<MessageInfo> Messages { get; } = [];

    public MainWindowViewModel(IAzureAuthService auth, IServiceBusService serviceBus)
    {
        _auth = auth;
        _serviceBus = serviceBus;
        _auth.AuthenticationChanged += (_, authenticated) => IsAuthenticated = authenticated;
    }

    public async Task InitializeAsync()
    {
        // Try to restore previous session using cached credentials
        IsLoading = true;
        StatusMessage = "Checking for saved credentials...";
        
        try
        {
            if (await _auth.TrySilentLoginAsync())
            {
                StatusMessage = "Loading subscriptions...";
                await LoadSubscriptionsAsync();
                StatusMessage = "Restored previous session";
            }
            else
            {
                StatusMessage = "Please sign in to continue";
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

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsLoading = true;
        StatusMessage = "Signing in to Azure...";
        
        try
        {
            if (await _auth.LoginAsync())
            {
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
        StatusMessage = "Signed out";
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
    private async Task SendMessageAsync()
    {
        if (SelectedNamespace == null || string.IsNullOrWhiteSpace(NewMessageBody)) return;
        
        string? entityName = SelectedQueue?.Name ?? SelectedTopic?.Name;
        if (entityName == null) return;
        
        IsLoading = true;
        StatusMessage = "Sending message...";
        
        try
        {
            await _serviceBus.SendMessageAsync(SelectedNamespace.Endpoint, entityName, NewMessageBody, null);
            NewMessageBody = "";
            StatusMessage = "Message sent";
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
    private async Task PurgeMessagesAsync()
    {
        if (SelectedNamespace == null) return;
        
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
            await _serviceBus.PurgeMessagesAsync(
                SelectedNamespace.Endpoint, entityName, subscription, ShowDeadLetter
            );
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
        if (SelectedNamespace != null)
            await SelectNamespaceAsync(SelectedNamespace);
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
}
