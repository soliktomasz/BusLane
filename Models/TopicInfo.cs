using System.Collections.ObjectModel;

namespace BusLane.Models;

public class TopicInfo
{
    public string Name { get; init; } = string.Empty;
    public long SizeInBytes { get; init; }
    public int SubscriptionCount { get; init; }
    public DateTimeOffset? AccessedAt { get; init; }
    public TimeSpan DefaultMessageTtl { get; init; }
    
    // Collection of subscriptions for this topic (loaded on demand)
    public ObservableCollection<SubscriptionInfo> Subscriptions { get; } = [];
    
    // Track if subscriptions have been loaded
    public bool SubscriptionsLoaded { get; set; }
    
    // For loading state
    public bool IsLoadingSubscriptions { get; set; }

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
