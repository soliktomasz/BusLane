namespace BusLane.ViewModels.Dashboard;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Coordinates the namespace priority inbox list and review-state deltas.
/// </summary>
public partial class NamespaceInboxViewModel : ViewModelBase
{
    private readonly INamespaceInboxScoringService _scoringService;
    private readonly INamespaceInboxReviewStore _reviewStore;
    private Action<NamespaceInboxItem> _openMessages;
    private Action<NamespaceInboxItem> _openDeadLetter;
    private Action<NamespaceInboxItem> _openSessionInspector;
    private string? _currentNamespaceId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandButtonText))]
    private bool _isExpanded = true;

    public ObservableCollection<NamespaceInboxItemViewModel> Items { get; } = [];
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => Items.Count == 0;
    public string ExpandButtonText => IsExpanded ? "Collapse" : "Expand";

    public NamespaceInboxViewModel(
        INamespaceInboxScoringService scoringService,
        INamespaceInboxReviewStore reviewStore)
        : this(scoringService, reviewStore, _ => { }, _ => { }, _ => { })
    {
    }

    public NamespaceInboxViewModel(
        INamespaceInboxScoringService scoringService,
        INamespaceInboxReviewStore reviewStore,
        Action<NamespaceInboxItem> openMessages,
        Action<NamespaceInboxItem> openDeadLetter,
        Action<NamespaceInboxItem> openSessionInspector)
    {
        _scoringService = scoringService;
        _reviewStore = reviewStore;
        _openMessages = openMessages;
        _openDeadLetter = openDeadLetter;
        _openSessionInspector = openSessionInspector;

        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
        };
    }

    public void UpdateActions(
        Action<NamespaceInboxItem> openMessages,
        Action<NamespaceInboxItem> openDeadLetter,
        Action<NamespaceInboxItem> openSessionInspector)
    {
        _openMessages = openMessages;
        _openDeadLetter = openDeadLetter;
        _openSessionInspector = openSessionInspector;
    }

    public void Refresh(
        string namespaceId,
        IEnumerable<QueueInfo> queues,
        IEnumerable<SubscriptionInfo> subscriptions,
        IEnumerable<AlertEvent> activeAlerts)
    {
        _currentNamespaceId = namespaceId;

        var rankedItems = _scoringService.Rank(queues, subscriptions, activeAlerts);

        Items.Clear();

        foreach (var item in rankedItems)
        {
            var reviewState = _reviewStore.Get(namespaceId, item.EntityName);
            Items.Add(new NamespaceInboxItemViewModel(
                item,
                activeMessageDelta: item.ActiveMessageCount - (reviewState?.ActiveMessageCount ?? item.ActiveMessageCount),
                deadLetterDelta: item.DeadLetterCount - (reviewState?.DeadLetterCount ?? item.DeadLetterCount),
                scheduledDelta: item.ScheduledCount - (reviewState?.ScheduledCount ?? item.ScheduledCount),
                alertDelta: item.ActiveAlertCount - (reviewState?.ActiveAlertCount ?? item.ActiveAlertCount),
                _openMessages,
                _openDeadLetter,
                _openSessionInspector,
                MarkReviewed));
        }
    }

    private void MarkReviewed(NamespaceInboxItem item)
    {
        if (string.IsNullOrWhiteSpace(_currentNamespaceId))
        {
            return;
        }

        _reviewStore.Save(new NamespaceInboxReviewState(
            _currentNamespaceId,
            item.EntityName,
            DateTimeOffset.UtcNow,
            item.ActiveMessageCount,
            item.DeadLetterCount,
            item.ScheduledCount,
            item.ActiveAlertCount));
    }

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
