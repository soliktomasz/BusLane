using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BusLane.Models;

public class TopicInfo : INotifyPropertyChanged
{
    private bool _subscriptionsLoaded;
    private bool _isLoadingSubscriptions;
    
    public string Name { get; init; } = string.Empty;
    public long SizeInBytes { get; init; }
    public int SubscriptionCount { get; init; }
    public DateTimeOffset? AccessedAt { get; init; }
    public TimeSpan DefaultMessageTtl { get; init; }
    
    // Collection of subscriptions for this topic (loaded on demand)
    public ObservableCollection<SubscriptionInfo> Subscriptions { get; } = [];
    
    // Track if subscriptions have been loaded
    public bool SubscriptionsLoaded
    {
        get => _subscriptionsLoaded;
        set => SetField(ref _subscriptionsLoaded, value);
    }
    
    // For loading state
    public bool IsLoadingSubscriptions
    {
        get => _isLoadingSubscriptions;
        set => SetField(ref _isLoadingSubscriptions, value);
    }

    public TopicInfo() { }
    
    public TopicInfo(string name, long sizeInBytes, int subscriptionCount, DateTimeOffset? accessedAt, TimeSpan defaultMessageTtl)
    {
        Name = name;
        SizeInBytes = sizeInBytes;
        SubscriptionCount = subscriptionCount;
        AccessedAt = accessedAt;
        DefaultMessageTtl = defaultMessageTtl;
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
