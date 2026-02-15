using BusLane.Models.Dashboard;
using BusLane.Services.Dashboard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BusLane.ViewModels.Dashboard;

public partial class NamespaceDashboardViewModel : ObservableObject
{
    private readonly IDashboardRefreshService _refreshService;

    [ObservableProperty]
    private string _selectedTimeRange = "1 Hour";

    [ObservableProperty]
    private bool _autoRefreshEnabled = true;

    [ObservableProperty]
    private int _refreshIntervalSeconds = 30;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private DateTimeOffset? _lastRefreshTime;

    // Metric Cards
    public MetricCardViewModel ActiveMessagesCard { get; }
    public MetricCardViewModel DeadLetterCard { get; }
    public MetricCardViewModel ScheduledCard { get; }
    public MetricCardViewModel SizeCard { get; }

    // Top Entities Lists
    public TopEntitiesListViewModel TopQueues { get; }
    public TopEntitiesListViewModel TopTopics { get; }

    // Charts
    public ObservableCollection<DashboardChartViewModel> Charts { get; }

    public string[] TimeRangeOptions { get; } = new[]
    {
        "15 Minutes",
        "1 Hour",
        "6 Hours",
        "24 Hours"
    };

    public NamespaceDashboardViewModel(IDashboardRefreshService refreshService)
    {
        _refreshService = refreshService;
        _refreshService.SummaryUpdated += OnSummaryUpdated;
        _refreshService.TopEntitiesUpdated += OnTopEntitiesUpdated;

        // Initialize metric cards
        ActiveMessagesCard = new MetricCardViewModel("Active Messages", "messages");
        DeadLetterCard = new MetricCardViewModel("Dead Letter Messages", "messages");
        ScheduledCard = new MetricCardViewModel("Scheduled Messages", "messages");
        SizeCard = new MetricCardViewModel("Total Size", "MB");

        // Initialize top entities lists
        TopQueues = new TopEntitiesListViewModel("Top Queues");
        TopTopics = new TopEntitiesListViewModel("Top Topics");

        // Initialize charts
        Charts = new ObservableCollection<DashboardChartViewModel>
        {
            new("Active Messages Over Time"),
            new("Dead Letters Over Time"),
            new("Scheduled Messages Over Time"),
            new("Size Over Time"),
            new("Connection Activity"),
            new("Message Throughput")
        };
    }

    partial void OnSelectedTimeRangeChanged(string value)
    {
        foreach (var chart in Charts)
        {
            chart.SetGlobalTimeRange(value);
        }
        _ = RefreshAsync();
    }

    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        // TODO: Start/stop auto refresh
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await _refreshService.RefreshAsync("current-namespace");
        }
        finally
        {
            IsRefreshing = false;
            LastRefreshTime = DateTimeOffset.Now;
        }
    }

    private void OnSummaryUpdated(object? sender, NamespaceDashboardSummary summary)
    {
        ActiveMessagesCard.UpdateValue(summary.TotalActiveMessages);
        DeadLetterCard.UpdateValue(summary.TotalDeadLetterMessages);
        ScheduledCard.UpdateValue(summary.TotalScheduledMessages);
        SizeCard.UpdateValue(summary.TotalSizeInBytes / (1024.0 * 1024.0)); // Convert to MB
    }

    private void OnTopEntitiesUpdated(object? sender, IReadOnlyList<TopEntityInfo> entities)
    {
        var queues = entities.Where(e => e.Type == EntityType.Queue).Take(10).ToList();
        var topics = entities.Where(e => e.Type == EntityType.Topic).Take(10).ToList();

        TopQueues.UpdateEntities(queues);
        TopTopics.UpdateEntities(topics);
    }

    public void Dispose()
    {
        _refreshService.SummaryUpdated -= OnSummaryUpdated;
        _refreshService.TopEntitiesUpdated -= OnTopEntitiesUpdated;
    }
}
