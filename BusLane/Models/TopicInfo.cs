namespace BusLane.Models;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Represents an Azure Service Bus topic with its properties.
/// Uses CommunityToolkit.Mvvm for observable properties.
/// </summary>
public partial class TopicInfo : ObservableObject
{
    private bool? _expandedBeforeSearch;
    private string _entityFilter = string.Empty;

    /// <summary>Subscriptions matching the browser search, retaining all children when the topic matches.</summary>
    public IEnumerable<SubscriptionInfo> FilteredSubscriptions =>
        string.IsNullOrEmpty(_entityFilter) || Name.Contains(_entityFilter, StringComparison.OrdinalIgnoreCase)
            ? Subscriptions
            : Subscriptions.Where(s => s.Name.Contains(_entityFilter, StringComparison.OrdinalIgnoreCase));

    /// <summary>Applies the browser filter without loading subscriptions from the network.</summary>
    public void ApplyEntityFilter(string filter)
    {
        _entityFilter = filter.Trim();
        if (_entityFilter.Length > 0)
        {
            _expandedBeforeSearch ??= IsExpanded;
            if (Subscriptions.Any(s => s.Name.Contains(_entityFilter, StringComparison.OrdinalIgnoreCase)))
                IsExpanded = true;
        }
        else if (_expandedBeforeSearch is { } expanded)
        {
            IsExpanded = expanded;
            _expandedBeforeSearch = null;
        }

        OnPropertyChanged(nameof(FilteredSubscriptions));
    }

    public string Name { get; init; } = string.Empty;
    public long SizeInBytes { get; init; }
    public DateTimeOffset? AccessedAt { get; init; }
    public TimeSpan DefaultMessageTtl { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
    private int _subscriptionCount;

    /// <summary>
    /// Collection of subscriptions for this topic (loaded on demand).
    /// </summary>
    public ObservableCollection<SubscriptionInfo> Subscriptions { get; } = [];

    /// <summary>
    /// Indicates whether subscriptions have been loaded.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
    private bool _subscriptionsLoaded;

    /// <summary>
    /// Indicates whether subscriptions are currently being loaded.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
    private bool _isLoadingSubscriptions;

    /// <summary>
    /// Indicates whether the topic is expanded to show subscriptions.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Display status text based on loading state.
    /// </summary>
    public string DisplayStatus => IsLoadingSubscriptions
        ? "Loading..."
        : SubscriptionsLoaded
            ? $"{Subscriptions.Count} subscription(s)"
            : "Click to expand";

    public TopicInfo()
    {
        Subscriptions.CollectionChanged += (_, _) =>
        {
            ApplyEntityFilter(_entityFilter);
            OnPropertyChanged(nameof(DisplayStatus));
        };
    }

    public TopicInfo(string name, long sizeInBytes, int subscriptionCount, DateTimeOffset? accessedAt, TimeSpan defaultMessageTtl)
        : this()
    {
        Name = name;
        SizeInBytes = sizeInBytes;
        SubscriptionCount = subscriptionCount;
        AccessedAt = accessedAt;
        DefaultMessageTtl = defaultMessageTtl;
    }
}
