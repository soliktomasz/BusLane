namespace BusLane.ViewModels.Dashboard;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Coordinates the namespace priority inbox list and review-state deltas.
/// </summary>
public partial class NamespaceInboxViewModel : ViewModelBase
{
    private const int MaxPriorityItems = 8;
    private readonly INamespaceInboxScoringService _scoringService;
    private readonly INamespaceInboxReviewStore _reviewStore;
    private Action<NamespaceNavigationRequest> _navigate;
    private string? _currentNamespaceId;
    private IReadOnlyList<NamespaceInboxItem> _latestRankedItems = [];
    private IReadOnlyDictionary<string, NamespaceInboxReviewState> _reviewStates =
        new Dictionary<string, NamespaceInboxReviewState>(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandButtonText))]
    private bool _isExpanded = true;

    public ObservableCollection<NamespaceInboxItemViewModel> PriorityItems { get; } = [];
    public ObservableCollection<NamespaceInboxItemViewModel> AllIssues { get; } = [];
    public ObservableCollection<NamespaceInboxItemViewModel> Items => PriorityItems;
    public int NeedsActionCount => PriorityItems.Count;
    public bool HasItems => PriorityItems.Count > 0;
    public bool IsEmpty => PriorityItems.Count == 0;
    public string ExpandButtonText => IsExpanded ? "Collapse" : "Expand";

    public NamespaceInboxViewModel(
        INamespaceInboxScoringService scoringService,
        INamespaceInboxReviewStore reviewStore)
        : this(scoringService, reviewStore, _ => { })
    {
    }

    public NamespaceInboxViewModel(
        INamespaceInboxScoringService scoringService,
        INamespaceInboxReviewStore reviewStore,
        Action<NamespaceNavigationRequest> navigate)
    {
        _scoringService = scoringService;
        _reviewStore = reviewStore;
        _navigate = navigate;

        PriorityItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(NeedsActionCount));
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
        };
    }

    public void UpdateNavigation(Action<NamespaceNavigationRequest> navigate)
    {
        _navigate = navigate;
    }

    public void Refresh(
        string namespaceId,
        IEnumerable<QueueInfo> queues,
        IEnumerable<SubscriptionInfo> subscriptions,
        IEnumerable<AlertEvent> activeAlerts)
    {
        _currentNamespaceId = namespaceId;

        _latestRankedItems = _scoringService.Rank(queues, subscriptions, activeAlerts);
        _reviewStates = _reviewStore.LoadAll()
            .Where(review => string.Equals(review.NamespaceId, namespaceId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(review => review.EntityName, StringComparer.OrdinalIgnoreCase);

        RebuildProjections();
    }

    private void RebuildProjections()
    {
        PriorityItems.Clear();
        AllIssues.Clear();

        foreach (var item in _latestRankedItems.Where(IsActionable))
        {
            _reviewStates.TryGetValue(item.EntityName, out var reviewState);
            var hasWorsened = reviewState is not null && HasWorsened(item, reviewState);
            var viewModel = CreateItemViewModel(item, reviewState, isReviewed: reviewState is not null && !hasWorsened);
            AllIssues.Add(viewModel);

            if ((reviewState is null || hasWorsened) && PriorityItems.Count < MaxPriorityItems)
            {
                PriorityItems.Add(viewModel);
            }
        }
    }

    private NamespaceInboxItemViewModel CreateItemViewModel(
        NamespaceInboxItem item,
        NamespaceInboxReviewState? reviewState,
        bool isReviewed) =>
        new(
            item,
            activeMessageDelta: item.ActiveMessageCount - (reviewState?.ActiveMessageCount ?? item.ActiveMessageCount),
            deadLetterDelta: item.DeadLetterCount - (reviewState?.DeadLetterCount ?? item.DeadLetterCount),
            scheduledDelta: item.ScheduledCount - (reviewState?.ScheduledCount ?? item.ScheduledCount),
            alertDelta: item.ActiveAlertCount - (reviewState?.ActiveAlertCount ?? item.ActiveAlertCount),
            isReviewed,
            _navigate,
            MarkReviewed);

    private void MarkReviewed(NamespaceInboxItem item)
    {
        if (string.IsNullOrWhiteSpace(_currentNamespaceId))
        {
            return;
        }

        var reviewState = new NamespaceInboxReviewState(
            _currentNamespaceId,
            item.EntityName,
            DateTimeOffset.UtcNow,
            item.ActiveMessageCount,
            item.DeadLetterCount,
            item.ScheduledCount,
            item.ActiveAlertCount);
        _reviewStore.Save(reviewState);

        var updatedStates = new Dictionary<string, NamespaceInboxReviewState>(_reviewStates, StringComparer.OrdinalIgnoreCase)
        {
            [item.EntityName] = reviewState
        };
        _reviewStates = updatedStates;
        RebuildProjections();
    }

    private static bool IsActionable(NamespaceInboxItem item) =>
        item.Score > 0 && item.Reasons.Count > 0;

    private static bool HasWorsened(NamespaceInboxItem item, NamespaceInboxReviewState review) =>
        item.ActiveMessageCount > review.ActiveMessageCount
        || item.DeadLetterCount > review.DeadLetterCount
        || item.ScheduledCount > review.ScheduledCount
        || item.ActiveAlertCount > review.ActiveAlertCount;

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
