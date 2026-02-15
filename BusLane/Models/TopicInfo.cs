namespace BusLane.Models;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Represents an Azure Service Bus topic with its properties.
/// Uses CommunityToolkit.Mvvm for observable properties.
/// </summary>
public partial class TopicInfo : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public long SizeInBytes { get; init; }
    public int SubscriptionCount { get; init; }
    public DateTimeOffset? AccessedAt { get; init; }
    public TimeSpan DefaultMessageTtl { get; init; }

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

    public TopicInfo() { }

    public TopicInfo(string name, long sizeInBytes, int subscriptionCount, DateTimeOffset? accessedAt, TimeSpan defaultMessageTtl)
    {
        Name = name;
        SizeInBytes = sizeInBytes;
        SubscriptionCount = subscriptionCount;
        AccessedAt = accessedAt;
        DefaultMessageTtl = defaultMessageTtl;
    }
}
