namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Services.Monitoring;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class MetricCardWidgetViewModel : DashboardWidgetViewModel
{
    private readonly IMetricsService _metricsService;

    [ObservableProperty] private double _currentValue;
    [ObservableProperty] private double _previousValue;
    [ObservableProperty] private bool _isTrendUp;
    [ObservableProperty] private string _trendPercentage = "0%";
    [ObservableProperty] private string _metricUnit = string.Empty;

    public MetricCardWidgetViewModel(DashboardWidget widget, IMetricsService metricsService) : base(widget)
    {
        _metricsService = metricsService;
        MetricUnit = GetMetricUnit();
        _metricsService.MetricRecorded += OnMetricRecorded;
        RefreshData();
    }

    private void OnMetricRecorded(object? sender, MetricDataPoint dataPoint)
    {
        ScheduleRefresh();
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();

            var duration = GetComparisonTimeSpan();
            var currentDuration = TimeSpan.FromMinutes(5);

            IEnumerable<MetricDataPoint> currentMetrics;
            IEnumerable<MetricDataPoint> previousMetrics;

            if (string.IsNullOrEmpty(Widget.Configuration.EntityFilter))
            {
                currentMetrics = _metricsService.GetAggregatedMetrics(Widget.Configuration.MetricName, currentDuration);
                previousMetrics = _metricsService.GetAggregatedMetrics(Widget.Configuration.MetricName, duration);
            }
            else
            {
                currentMetrics = _metricsService.GetMetricHistory(Widget.Configuration.EntityFilter, Widget.Configuration.MetricName, currentDuration);
                previousMetrics = _metricsService.GetMetricHistory(Widget.Configuration.EntityFilter, Widget.Configuration.MetricName, duration);
            }

            CurrentValue = currentMetrics.Any() ? currentMetrics.Average(m => m.Value) : 0;
            var previousRaw = previousMetrics.Any() ? previousMetrics.Average(m => m.Value) : 0;

            if (previousRaw > 0)
            {
                var change = (CurrentValue - previousRaw) / previousRaw;
                IsTrendUp = change > 0;
                TrendPercentage = $"{Math.Abs(change) * 100:F1}%";
            }
            else
            {
                IsTrendUp = CurrentValue > 0;
                TrendPercentage = CurrentValue > 0 ? "New" : "0%";
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return GetMetricDisplayName();
    }

    private string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Active Messages",
            "DeadLetterCount" => "Dead Letters",
            "ScheduledCount" => "Scheduled Messages",
            "SizeInBytes" => "Queue Size",
            _ => Widget.Configuration.MetricName
        };
    }

    private string GetMetricUnit()
    {
        return Widget.Configuration.MetricName == "SizeInBytes" ? "bytes" : "messages";
    }

    private TimeSpan GetComparisonTimeSpan()
    {
        return Widget.Configuration.TimeRange switch
        {
            "Previous Hour" => TimeSpan.FromHours(1),
            "Previous Day" => TimeSpan.FromDays(1),
            _ => TimeSpan.FromHours(1)
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _metricsService.MetricRecorded -= OnMetricRecorded;
        }
        base.Dispose(disposing);
    }
}
