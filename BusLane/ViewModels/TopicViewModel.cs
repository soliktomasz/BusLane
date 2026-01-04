using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels;

/// <summary>
/// ViewModel wrapper for TopicInfo that provides additional UI state management.
/// Use this when you need to extend TopicInfo functionality for specific views.
/// For basic usage, TopicInfo itself now includes observable properties.
/// </summary>
public class TopicViewModel : ObservableObject
{
    /// <summary>
    /// The underlying topic information.
    /// </summary>
    public TopicInfo Info { get; }

    #region Forwarded Properties
    
    // Expose TopicInfo properties for convenience
    public string Name => Info.Name;
    public long SizeInBytes => Info.SizeInBytes;
    public int SubscriptionCount => Info.SubscriptionCount;
    public DateTimeOffset? AccessedAt => Info.AccessedAt;
    public TimeSpan DefaultMessageTtl => Info.DefaultMessageTtl;
    public ObservableCollection<SubscriptionInfo> Subscriptions => Info.Subscriptions;
    
    // Delegate to TopicInfo's observable properties
    public bool SubscriptionsLoaded
    {
        get => Info.SubscriptionsLoaded;
        set => Info.SubscriptionsLoaded = value;
    }

    public bool IsLoadingSubscriptions
    {
        get => Info.IsLoadingSubscriptions;
        set => Info.IsLoadingSubscriptions = value;
    }

    public string DisplayStatus => Info.DisplayStatus;
    
    #endregion

    private static readonly string[] ForwardedProperties =
    [
        nameof(TopicInfo.SubscriptionsLoaded),
        nameof(TopicInfo.IsLoadingSubscriptions),
        nameof(TopicInfo.DisplayStatus)
    ];

    public TopicViewModel(TopicInfo info)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        
        // Forward property changes from Info to this ViewModel using PropertyForwarder
        this.CreateForwarder(OnPropertyChanged)
            .ForwardWithHandlers(Info, ForwardedProperties, new Dictionary<string, Action>
            {
                [nameof(TopicInfo.SubscriptionsLoaded)] = () => OnPropertyChanged(nameof(DisplayStatus)),
                [nameof(TopicInfo.IsLoadingSubscriptions)] = () => OnPropertyChanged(nameof(DisplayStatus))
            });
    }

    /// <summary>
    /// Creates a TopicViewModel from a TopicInfo instance.
    /// </summary>
    public static TopicViewModel FromTopicInfo(TopicInfo info) => new(info);
}

