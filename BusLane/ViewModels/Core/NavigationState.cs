namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;
using System.ComponentModel;
using BusLane.Models;
using BusLane.Services.Abstractions;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Holds the current navigation state - selected entities and collections.
/// Single source of truth for what's currently selected in the UI.
/// </summary>
public partial class NavigationState : ViewModelBase
{
    private readonly IPreferencesService? _preferencesService;
    private readonly List<PinnedEntity> _allPinnedEntities = [];
    private readonly ObservableCollection<PinnedEntity> _pinnedEntities = [];
    private string? _pinScopeId;
    private readonly HashSet<TopicInfo> _observedTopics = [];

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
    /// Gets the pinned entities visible in the current workspace scope.
    /// </summary>
    public ReadOnlyObservableCollection<PinnedEntity> PinnedEntities { get; }

    /// <summary>
    /// Gets the filtered queues based on the current entity filter text.
    /// </summary>
    public IEnumerable<QueueInfo> FilteredQueues =>
        string.IsNullOrWhiteSpace(EntityFilter)
            ? Queues
            : Queues.Where(q =>
                q.Name.Contains(EntityFilter.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the filtered topics based on the current entity filter text.
    /// </summary>
    public IEnumerable<TopicInfo> FilteredTopics =>
        string.IsNullOrWhiteSpace(EntityFilter)
            ? Topics
            : Topics.Where(t =>
                t.Name.Contains(EntityFilter.Trim(), StringComparison.OrdinalIgnoreCase) ||
                t.Subscriptions.Any(s => s.Name.Contains(EntityFilter.Trim(), StringComparison.OrdinalIgnoreCase)));

    /// <summary>Whether a nonempty browser query has no matching loaded entities.</summary>
    public bool HasNoMatchingEntities => !string.IsNullOrWhiteSpace(EntityFilter) &&
        !FilteredQueues.Any() && !FilteredTopics.Any();

    /// <summary>Whether subscription search is limited by topics that have not yet been expanded.</summary>
    public bool HasUnloadedTopics => Topics.Any(t => !t.SubscriptionsLoaded);

    /// <summary>Whether the selected-entity footer is visible.</summary>
    public bool HasSelectedEntity => SelectedEntity is QueueInfo or TopicInfo or SubscriptionInfo;

    /// <summary>Human-readable selected entity type.</summary>
    public string SelectedEntityKind => SelectedEntity switch
    {
        QueueInfo => "Queue",
        TopicInfo => "Topic",
        SubscriptionInfo => "Subscription",
        _ => string.Empty
    };

    /// <summary>Full entity path, including the parent topic for subscriptions.</summary>
    public string SelectedEntityPath => SelectedEntity switch
    {
        QueueInfo q => q.Name,
        TopicInfo t => t.Name,
        SubscriptionInfo s => $"{s.TopicName} / {s.Name}",
        _ => string.Empty
    };

    /// <summary>Secondary metrics for the selected entity, leaving names uncluttered in the tree.</summary>
    public string SelectedEntitySummary => SelectedEntity switch
    {
        QueueInfo q => $"{q.ActiveMessageCount:N0} active · {q.DeadLetterCount:N0} dead-letter · {q.ScheduledCount:N0} scheduled",
        SubscriptionInfo s => $"{s.ActiveMessageCount:N0} active · {s.DeadLetterCount:N0} dead-letter",
        TopicInfo t => $"{t.SubscriptionCount:N0} subscriptions",
        _ => string.Empty
    };

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

    /// <summary>
    /// Gets whether the current workspace scope has visible pinned entities.
    /// </summary>
    public bool HasPinnedEntities => PinnedEntities.Count > 0;
    public long TotalDeadLetterCount => Queues.Sum(q => q.DeadLetterCount) + TopicSubscriptions.Sum(s => s.DeadLetterCount);
    public bool HasDeadLetters => TotalDeadLetterCount > 0;
    public long CurrentActiveMessageCount => SelectedQueue?.ActiveMessageCount ?? SelectedSubscription?.ActiveMessageCount ?? 0;
    public long CurrentDeadLetterCount => SelectedQueue?.DeadLetterCount ?? SelectedSubscription?.DeadLetterCount ?? 0;
    public bool CanShowSessionInspector => CurrentEntityRequiresSession && !string.IsNullOrWhiteSpace(CurrentEntityName);
    public bool IsSessionInspectorTabSelected => SelectedMessageTabIndex == 2;

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

    /// <summary>
    /// Gets whether the currently selected entity is pinned in the current workspace scope.
    /// </summary>
    public bool IsSelectedEntityPinned => SelectedEntity != null && IsPinned(SelectedEntity);

    /// <summary>
    /// Initializes a navigation state without persisted pin support.
    /// </summary>
    /// <remarks>
    /// Pin changes are kept in memory only when no preferences service is supplied.
    /// </remarks>
    public NavigationState()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a navigation state with optional persisted pin support.
    /// </summary>
    /// <param name="preferencesService">
    /// Preferences store used to load and save pinned entities; when null, pins are kept in memory only.
    /// </param>
    public NavigationState(IPreferencesService? preferencesService)
    {
        _preferencesService = preferencesService;
        PinnedEntities = new ReadOnlyObservableCollection<PinnedEntity>(_pinnedEntities);
        LoadAllPinnedEntities();

        Queues.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasQueues));
            OnPropertyChanged(nameof(FilteredQueues));
            OnPropertyChanged(nameof(HasNoMatchingEntities));
            OnPropertyChanged(nameof(TotalDeadLetterCount));
            OnPropertyChanged(nameof(HasDeadLetters));
        };
        Topics.CollectionChanged += (_, _) =>
        {
            foreach (var removed in _observedTopics.Except(Topics).ToList())
            {
                removed.PropertyChanged -= OnTopicPropertyChanged;
                _observedTopics.Remove(removed);
                removed.ApplyEntityFilter(string.Empty);
            }
            foreach (var added in Topics.Except(_observedTopics).ToList())
            {
                _observedTopics.Add(added);
                added.ApplyEntityFilter(EntityFilter);
                added.PropertyChanged += OnTopicPropertyChanged;
            }
            OnPropertyChanged(nameof(HasTopics));
            OnPropertyChanged(nameof(FilteredTopics));
            OnPropertyChanged(nameof(HasNoMatchingEntities));
            OnPropertyChanged(nameof(HasUnloadedTopics));
        };
        TopicSubscriptions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TotalDeadLetterCount));
            OnPropertyChanged(nameof(HasDeadLetters));
        };
        _pinnedEntities.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPinnedEntities));
            OnPropertyChanged(nameof(IsSelectedEntityPinned));
        };
        Namespaces.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(FilteredNamespaces));
        };
    }

    partial void OnEntityFilterChanged(string value)
    {
        foreach (var topic in Topics)
            topic.ApplyEntityFilter(value);
        if (!string.IsNullOrWhiteSpace(value))
        {
            IsQueuesSectionExpanded = true;
            IsTopicsSectionExpanded = true;
        }
        OnPropertyChanged(nameof(FilteredQueues));
        OnPropertyChanged(nameof(FilteredTopics));
        OnPropertyChanged(nameof(HasNoMatchingEntities));
    }

    private void OnTopicPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TopicInfo.FilteredSubscriptions))
        {
            OnPropertyChanged(nameof(FilteredTopics));
            OnPropertyChanged(nameof(HasNoMatchingEntities));
        }
        if (e.PropertyName == nameof(TopicInfo.SubscriptionsLoaded))
            OnPropertyChanged(nameof(HasUnloadedTopics));
        if (ReferenceEquals(sender, SelectedEntity) && e.PropertyName == nameof(TopicInfo.SubscriptionCount))
            OnPropertyChanged(nameof(SelectedEntitySummary));
    }

    partial void OnNamespaceFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredNamespaces));
    }

    partial void OnSelectedMessageTabIndexChanged(int value)
    {
        ShowDeadLetter = value == 1;
        OnPropertyChanged(nameof(IsSessionInspectorTabSelected));
    }

    partial void OnSelectedQueueChanged(QueueInfo? value)
    {
        _ = value;
        OnCurrentEntitySelectionChanged();
    }

    partial void OnSelectedTopicChanged(TopicInfo? value)
    {
        _ = value;
        OnCurrentEntitySelectionChanged();
    }

    partial void OnSelectedSubscriptionChanged(SubscriptionInfo? value)
    {
        _ = value;
        OnCurrentEntitySelectionChanged();
    }

    partial void OnSelectedEntityChanged(object? value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsSelectedEntityPinned));
        OnPropertyChanged(nameof(HasSelectedEntity));
        OnPropertyChanged(nameof(SelectedEntityKind));
        OnPropertyChanged(nameof(SelectedEntityPath));
        OnPropertyChanged(nameof(SelectedEntitySummary));
    }

    /// <summary>
    /// Clears all navigation state.
    /// </summary>
    public void Clear()
    {
        SetPinScope(null);
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

    /// <summary>
    /// Sets the workspace scope used to filter and persist pinned entities.
    /// </summary>
    /// <param name="scopeId">Workspace identifier, or null/empty to clear visible pins.</param>
    public void SetPinScope(string? scopeId)
    {
        _pinScopeId = string.IsNullOrWhiteSpace(scopeId) ? null : scopeId;
        ReloadScopedPins();
    }

    /// <summary>
    /// Pins or unpins the supplied queue, topic, subscription, or existing pin.
    /// </summary>
    /// <param name="entity">Entity to toggle; unsupported values are ignored.</param>
    /// <remarks>
    /// Persistence failures roll back in-memory pin state before rethrowing.
    /// </remarks>
    public void TogglePin(object? entity)
    {
        var pin = CreatePin(entity);
        if (pin == null)
        {
            return;
        }

        var previousPins = _allPinnedEntities.ToList();
        var previousJson = _preferencesService?.PinnedEntitiesJson;
        var existing = _allPinnedEntities.FirstOrDefault(item => item == pin);
        if (existing == null)
        {
            _allPinnedEntities.Add(pin);
        }
        else
        {
            _allPinnedEntities.Remove(existing);
        }

        try
        {
            PersistPins();
            ReloadScopedPins();
        }
        catch
        {
            _allPinnedEntities.Clear();
            _allPinnedEntities.AddRange(previousPins);
            if (_preferencesService != null)
            {
                _preferencesService.PinnedEntitiesJson = previousJson ?? "[]";
            }

            ReloadScopedPins();
            throw;
        }
    }

    /// <summary>
    /// Gets whether the supplied entity is pinned in the current workspace scope.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True when the entity is pinned in the active scope; otherwise false.</returns>
    public bool IsPinned(object? entity)
    {
        var pin = CreatePin(entity);
        return pin != null && PinnedEntities.Contains(pin);
    }

    private void OnCurrentEntitySelectionChanged()
    {
        OnPropertyChanged(nameof(CurrentEntityName));
        OnPropertyChanged(nameof(CurrentSubscriptionName));
        OnPropertyChanged(nameof(CurrentEntityRequiresSession));
        OnPropertyChanged(nameof(CurrentActiveMessageCount));
        OnPropertyChanged(nameof(CurrentDeadLetterCount));
        OnPropertyChanged(nameof(CanShowSessionInspector));
        OnPropertyChanged(nameof(IsSelectedEntityPinned));

        if (!CurrentEntityRequiresSession && SelectedMessageTabIndex == 2)
        {
            SelectedMessageTabIndex = 0;
        }
    }

    private PinnedEntity? CreatePin(object? entity)
    {
        if (string.IsNullOrWhiteSpace(_pinScopeId))
        {
            return null;
        }

        return entity switch
        {
            QueueInfo queue => new PinnedEntity(_pinScopeId, PinnedEntityType.Queue, queue.Name, null),
            TopicInfo topic => new PinnedEntity(_pinScopeId, PinnedEntityType.Topic, topic.Name, null),
            SubscriptionInfo subscription => new PinnedEntity(_pinScopeId, PinnedEntityType.Subscription, subscription.Name, subscription.TopicName),
            PinnedEntity pin when pin.WorkspaceId == _pinScopeId => pin,
            _ => null
        };
    }

    private void LoadAllPinnedEntities()
    {
        _allPinnedEntities.Clear();
        if (_preferencesService == null)
        {
            return;
        }

        try
        {
            _allPinnedEntities.AddRange(DeserializeList<PinnedEntity>(_preferencesService.PinnedEntitiesJson));
        }
        catch
        {
            // Invalid preference data should not block navigation.
        }
    }

    private void PersistPins()
    {
        if (_preferencesService == null)
        {
            return;
        }

        _preferencesService.PinnedEntitiesJson = Serialize(_allPinnedEntities);
        _preferencesService.Save();
    }

    private void ReloadScopedPins()
    {
        _pinnedEntities.Clear();
        if (!string.IsNullOrWhiteSpace(_pinScopeId))
        {
            foreach (var pin in _allPinnedEntities.Where(pin => pin.WorkspaceId == _pinScopeId))
            {
                _pinnedEntities.Add(pin);
            }
        }

        OnPropertyChanged(nameof(FilteredQueues));
        OnPropertyChanged(nameof(FilteredTopics));
    }
}
