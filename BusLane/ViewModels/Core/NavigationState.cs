using System.Collections.ObjectModel;
using BusLane.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Holds the current navigation state - selected entities and collections.
/// Single source of truth for what's currently selected in the UI.
/// </summary>
public partial class NavigationState : ViewModelBase
{
    [ObservableProperty] private AzureSubscription? _selectedAzureSubscription;
    [ObservableProperty] private ServiceBusNamespace? _selectedNamespace;
    [ObservableProperty] private object? _selectedEntity;
    [ObservableProperty] private QueueInfo? _selectedQueue;
    [ObservableProperty] private TopicInfo? _selectedTopic;
    [ObservableProperty] private SubscriptionInfo? _selectedSubscription;
    [ObservableProperty] private bool _showDeadLetter;
    [ObservableProperty] private int _selectedMessageTabIndex;
    [ObservableProperty] private string _namespaceFilter = string.Empty;
    [ObservableProperty] private string _entityFilter = string.Empty;
    [ObservableProperty] private bool _isQueuesSectionExpanded = true;
    [ObservableProperty] private bool _isTopicsSectionExpanded = true;

    public ObservableCollection<AzureSubscription> Subscriptions { get; } = [];
    public ObservableCollection<ServiceBusNamespace> Namespaces { get; } = [];
    public ObservableCollection<QueueInfo> Queues { get; } = [];
    public ObservableCollection<TopicInfo> Topics { get; } = [];
    public ObservableCollection<SubscriptionInfo> TopicSubscriptions { get; } = [];

    /// <summary>
    /// Gets the filtered queues based on the current entity filter text.
    /// </summary>
    public IEnumerable<QueueInfo> FilteredQueues =>
        string.IsNullOrWhiteSpace(EntityFilter)
            ? Queues
            : Queues.Where(q =>
                q.Name.Contains(EntityFilter, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the filtered topics based on the current entity filter text.
    /// </summary>
    public IEnumerable<TopicInfo> FilteredTopics =>
        string.IsNullOrWhiteSpace(EntityFilter)
            ? Topics
            : Topics.Where(t =>
                t.Name.Contains(EntityFilter, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the filtered namespaces based on the current filter text.
    /// </summary>
    public IEnumerable<ServiceBusNamespace> FilteredNamespaces =>
        string.IsNullOrWhiteSpace(NamespaceFilter)
            ? Namespaces
            : Namespaces.Where(n =>
                n.Name.Contains(NamespaceFilter, StringComparison.OrdinalIgnoreCase) ||
                n.Location.Contains(NamespaceFilter, StringComparison.OrdinalIgnoreCase) ||
                n.ResourceGroup.Contains(NamespaceFilter, StringComparison.OrdinalIgnoreCase));

    // Computed properties for visibility bindings
    public bool HasQueues => Queues.Count > 0;
    public bool HasTopics => Topics.Count > 0;
    public long TotalDeadLetterCount => Queues.Sum(q => q.DeadLetterCount) + TopicSubscriptions.Sum(s => s.DeadLetterCount);
    public bool HasDeadLetters => TotalDeadLetterCount > 0;

    /// <summary>
    /// Gets the current endpoint based on mode and selection.
    /// </summary>
    public string? CurrentEndpoint => SelectedNamespace?.Endpoint;

    /// <summary>
    /// Gets the current entity name for message operations.
    /// </summary>
    public string? CurrentEntityName => SelectedQueue?.Name 
        ?? SelectedSubscription?.TopicName 
        ?? SelectedTopic?.Name;

    /// <summary>
    /// Gets the current subscription name if a topic subscription is selected.
    /// </summary>
    public string? CurrentSubscriptionName => SelectedSubscription?.Name;

    /// <summary>
    /// Gets whether the current entity requires sessions.
    /// </summary>
    public bool CurrentEntityRequiresSession => 
        SelectedQueue?.RequiresSession ?? SelectedSubscription?.RequiresSession ?? false;

    public NavigationState()
    {
        Queues.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasQueues));
            OnPropertyChanged(nameof(FilteredQueues));
            OnPropertyChanged(nameof(TotalDeadLetterCount));
            OnPropertyChanged(nameof(HasDeadLetters));
        };
        Topics.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTopics));
            OnPropertyChanged(nameof(FilteredTopics));
        };
        TopicSubscriptions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TotalDeadLetterCount));
            OnPropertyChanged(nameof(HasDeadLetters));
        };
        Namespaces.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(FilteredNamespaces));
        };
    }

    partial void OnEntityFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredQueues));
        OnPropertyChanged(nameof(FilteredTopics));
    }

    partial void OnNamespaceFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredNamespaces));
    }

    partial void OnSelectedMessageTabIndexChanged(int value)
    {
        ShowDeadLetter = value == 1;
    }

    /// <summary>
    /// Clears all navigation state.
    /// </summary>
    public void Clear()
    {
        Subscriptions.Clear();
        Namespaces.Clear();
        Queues.Clear();
        Topics.Clear();
        TopicSubscriptions.Clear();
        SelectedNamespace = null;
        SelectedEntity = null;
        SelectedQueue = null;
        SelectedTopic = null;
        SelectedSubscription = null;
        SelectedAzureSubscription = null;
    }

    /// <summary>
    /// Clears entity-level state (queues, topics, messages) but keeps namespace.
    /// </summary>
    public void ClearEntities()
    {
        Queues.Clear();
        Topics.Clear();
        TopicSubscriptions.Clear();
        SelectedQueue = null;
        SelectedTopic = null;
        SelectedSubscription = null;
        SelectedEntity = null;
    }
}

