using BusLane.Models.Dashboard;
using BusLane.Services.Dashboard;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using LiveChartsCore.Defaults;
using System.Collections.ObjectModel;

namespace BusLane.ViewModels.Dashboard;

public partial class NamespaceDashboardViewModel : ObservableObject
{
    private readonly IDashboardRefreshService _refreshService;
    private IServiceBusOperations? _operations;
    private readonly List<NamespaceDashboardSummary> _summaryHistory = [];

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

    [ObservableProperty]
    private string? _currentNamespaceId;

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

    public int[] RefreshIntervalOptions { get; } = [5, 10, 15, 30, 60, 120, 300];

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
            new("Total Size (MB) Over Time")
        };

        foreach (var chart in Charts)
        {
            chart.TimeRangeChanged += OnChartTimeRangeChanged;
            chart.SetGlobalTimeRange(SelectedTimeRange);
        }
    }

    partial void OnSelectedTimeRangeChanged(string value)
    {
        foreach (var chart in Charts)
        {
            chart.SetGlobalTimeRange(value);
        }

        UpdateCharts();
        _ = RefreshAsync();
    }

    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        if (value && _operations != null)
        {
            _refreshService.StartAutoRefresh(
                CurrentNamespaceId ?? "current-namespace",
                _operations,
                TimeSpan.FromSeconds(RefreshIntervalSeconds));
        }
        else
        {
            _refreshService.StopAutoRefresh();
        }
    }

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        if (AutoRefreshEnabled && _operations != null)
        {
            _refreshService.StartAutoRefresh(
                CurrentNamespaceId ?? "current-namespace",
                _operations,
                TimeSpan.FromSeconds(value));
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            var namespaceId = CurrentNamespaceId ?? "current-namespace";
            await _refreshService.RefreshAsync(namespaceId, _operations);
        }
        finally
        {
            IsRefreshing = false;
            LastRefreshTime = DateTimeOffset.Now;
        }
    }

    /// <summary>
    /// Sets the Service Bus operations instance and triggers a refresh.
    /// Called when connecting to a namespace.
    /// </summary>
    public void SetOperations(IServiceBusOperations? operations, string? namespaceId = null)
    {
        _operations = operations;

        if (!string.IsNullOrEmpty(namespaceId))
        {
            CurrentNamespaceId = namespaceId;
        }

        if (_operations != null)
        {
            _ = RefreshAsync();

            if (AutoRefreshEnabled)
            {
                _refreshService.StartAutoRefresh(
                    CurrentNamespaceId ?? "current-namespace",
                    _operations,
                    TimeSpan.FromSeconds(RefreshIntervalSeconds));
            }
        }
        else
        {
            _refreshService.StopAutoRefresh();
        }
    }

    private void OnSummaryUpdated(object? sender, NamespaceDashboardSummary summary)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnSummaryUpdated(sender, summary));
            return;
        }

        _summaryHistory.Add(summary);
        PruneHistory();

        ActiveMessagesCard.UpdateValue(summary.TotalActiveMessages);
        DeadLetterCard.UpdateValue(summary.TotalDeadLetterMessages);
        ScheduledCard.UpdateValue(summary.TotalScheduledMessages);
        SizeCard.UpdateValue(summary.TotalSizeInBytes / (1024.0 * 1024.0)); // Convert to MB

        UpdateCharts();
    }

    private void OnTopEntitiesUpdated(object? sender, IReadOnlyList<TopEntityInfo> entities)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnTopEntitiesUpdated(sender, entities));
            return;
        }

        var queues = entities.Where(e => e.Type == EntityType.Queue).Take(10).ToList();
        var topics = entities.Where(e => e.Type == EntityType.Topic).Take(10).ToList();

        TopQueues.UpdateEntities(queues);
        TopTopics.UpdateEntities(topics);
    }

    public void Dispose()
    {
        foreach (var chart in Charts)
        {
            chart.TimeRangeChanged -= OnChartTimeRangeChanged;
        }

        _refreshService.SummaryUpdated -= OnSummaryUpdated;
        _refreshService.TopEntitiesUpdated -= OnTopEntitiesUpdated;
    }

    private void OnChartTimeRangeChanged(object? sender, string value)
    {
        UpdateCharts();
    }

    private void PruneHistory()
    {
        var threshold = DateTimeOffset.UtcNow - TimeSpan.FromHours(24);
        _summaryHistory.RemoveAll(s => s.Timestamp < threshold);
    }

    private void UpdateCharts()
    {
        if (Charts.Count < 4 || _summaryHistory.Count == 0)
        {
            return;
        }
        var activeHistory = GetHistoryForRange(Charts[0].SelectedTimeRange);
        Charts[0].UpdateData(activeHistory.Select(s => new DateTimePoint(s.Timestamp.LocalDateTime, s.TotalActiveMessages)));

        var deadLetterHistory = GetHistoryForRange(Charts[1].SelectedTimeRange);
        Charts[1].UpdateData(deadLetterHistory.Select(s => new DateTimePoint(s.Timestamp.LocalDateTime, s.TotalDeadLetterMessages)));

        var scheduledHistory = GetHistoryForRange(Charts[2].SelectedTimeRange);
        Charts[2].UpdateData(scheduledHistory.Select(s => new DateTimePoint(s.Timestamp.LocalDateTime, s.TotalScheduledMessages)));

        var sizeHistory = GetHistoryForRange(Charts[3].SelectedTimeRange);
        Charts[3].UpdateData(sizeHistory.Select(s => new DateTimePoint(s.Timestamp.LocalDateTime, s.TotalSizeInBytes / (1024.0 * 1024.0))));
    }

    private static TimeSpan GetTimeSpan(string selectedTimeRange)
    {
        return selectedTimeRange switch
        {
            "15 Minutes" => TimeSpan.FromMinutes(15),
            "1 Hour" => TimeSpan.FromHours(1),
            "6 Hours" => TimeSpan.FromHours(6),
            "24 Hours" => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(1)
        };
    }

    private IReadOnlyList<NamespaceDashboardSummary> GetHistoryForRange(string timeRange)
    {
        var now = DateTimeOffset.UtcNow;
        var timeSpan = GetTimeSpan(timeRange);

        return _summaryHistory
            .Where(s => s.Timestamp >= now - timeSpan)
            .OrderBy(s => s.Timestamp)
            .ToList();
    }
}
