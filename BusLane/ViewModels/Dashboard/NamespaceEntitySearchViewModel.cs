namespace BusLane.ViewModels.Dashboard;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>Performs deterministic in-memory search over current namespace inventory.</summary>
public partial class NamespaceEntitySearchViewModel : ViewModelBase
{
    private const int MaxResults = 30;
    private Action<NamespaceNavigationRequest> _navigate;
    private IReadOnlyList<NamespaceEntitySearchResult> _inventory = [];

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private NamespaceEntitySearchResult? _selectedResult;

    public ObservableCollection<NamespaceEntitySearchResult> Results { get; } = [];
    public bool HasResults => Results.Count > 0;

    public NamespaceEntitySearchViewModel(Action<NamespaceNavigationRequest> navigate)
    {
        _navigate = navigate;
        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasResults));
    }

    public void UpdateNavigation(Action<NamespaceNavigationRequest> navigate) => _navigate = navigate;

    public void UpdateInventory(
        IEnumerable<QueueInfo> queues,
        IEnumerable<TopicInfo> topics,
        IEnumerable<SubscriptionInfo> subscriptions)
    {
        _inventory = queues.Select(queue => CreateResult(EntityType.Queue, queue.Name, null))
            .Concat(topics.Select(topic => CreateResult(EntityType.Topic, topic.Name, null)))
            .Concat(subscriptions.Select(subscription => CreateResult(
                EntityType.Subscription,
                $"{subscription.TopicName}/{subscription.Name}",
                subscription.TopicName)))
            .ToList();
        RefreshResults();
    }

    partial void OnQueryChanged(string value) => RefreshResults();

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedResult is not null)
        {
            _navigate(SelectedResult.Request);
        }
    }

    [RelayCommand]
    private void OpenResult(NamespaceEntitySearchResult? result)
    {
        if (result is not null)
        {
            _navigate(result.Request);
        }
    }

    public void MoveSelection(int offset)
    {
        if (Results.Count == 0) return;
        var currentIndex = SelectedResult is null ? -1 : Results.IndexOf(SelectedResult);
        var nextIndex = Math.Clamp(currentIndex + offset, 0, Results.Count - 1);
        SelectedResult = Results[nextIndex];
    }

    public void Clear()
    {
        Query = string.Empty;
        SelectedResult = null;
    }

    private void RefreshResults()
    {
        Results.Clear();
        SelectedResult = null;
        var query = Query.Trim();
        if (query.Length == 0) return;

        foreach (var result in _inventory
                     .Select(item => (Item: item, Rank: GetMatchRank(item.DisplayPath, query)))
                     .Where(match => match.Rank != int.MaxValue)
                     .OrderBy(match => match.Rank)
                     .ThenBy(match => match.Item.TypeLabel, StringComparer.Ordinal)
                     .ThenBy(match => match.Item.DisplayPath, StringComparer.OrdinalIgnoreCase)
                     .Take(MaxResults)
                     .Select(match => match.Item))
        {
            Results.Add(result);
        }

        SelectedResult = Results.FirstOrDefault();
    }

    private static NamespaceEntitySearchResult CreateResult(EntityType type, string path, string? topicName)
    {
        var view = type == EntityType.Topic
            ? EntityWorkspaceView.TopicSubscriptions
            : EntityWorkspaceView.ActiveMessages;
        return new NamespaceEntitySearchResult(
            path,
            path,
            type.ToString(),
            new NamespaceNavigationRequest(type, path, topicName, view));
    }

    private static int GetMatchRank(string candidate, string query)
    {
        if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (candidate.Contains(query, StringComparison.OrdinalIgnoreCase)) return 1;
        return IsSubsequence(candidate, query) ? 2 : int.MaxValue;
    }

    private static bool IsSubsequence(string candidate, string query)
    {
        var queryIndex = 0;
        foreach (var character in candidate)
        {
            if (queryIndex < query.Length
                && char.ToUpperInvariant(character) == char.ToUpperInvariant(query[queryIndex]))
            {
                queryIndex++;
            }
        }

        return queryIndex == query.Length;
    }
}
