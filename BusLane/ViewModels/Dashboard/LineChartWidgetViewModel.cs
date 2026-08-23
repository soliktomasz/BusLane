namespace BusLane.ViewModels.Dashboard;

using System;
using System.Collections.Generic;
using System.Linq;
using BusLane.Models;
using BusLane.Models.Dashboard;
using BusLane.Services.Monitoring;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class LineChartWidgetViewModel : DashboardWidgetViewModel
{
    private readonly IMetricsService _metricsService;

    [ObservableProperty]
    private LinePlotData? _plotData;

    public LineChartWidgetViewModel(DashboardWidget widget, IMetricsService metricsService) : base(widget)
    {
        _metricsService = metricsService;

        _metricsService.MetricsBatchRecorded += OnMetricsBatchRecorded;
        RefreshData();
    }

    private void OnMetricsBatchRecorded(object? sender, IReadOnlyList<MetricDataPoint> metrics)
    {
        _ = sender;
        _ = metrics;
        ScheduleRefresh();
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();
            var duration = GetTimeSpan();

            IEnumerable<MetricDataPoint> metrics;
            if (string.IsNullOrEmpty(Widget.Configuration.EntityFilter))
            {
                metrics = _metricsService.GetAggregatedMetrics(Widget.Configuration.MetricName, duration);
            }
            else
            {
                metrics = _metricsService.GetMetricHistory(Widget.Configuration.EntityFilter, Widget.Configuration.MetricName, duration);
            }

            var points = metrics
                .GroupBy(m => new DateTime(m.Timestamp.Year, m.Timestamp.Month, m.Timestamp.Day,
                    m.Timestamp.Hour, m.Timestamp.Minute / 5 * 5, 0))
                .Select(g => new LinePlotPoint(g.Key, g.Sum(m => m.Value)))
                .OrderBy(p => p.Time)
                .ToList();

            PlotData = new LinePlotData(Title, points, GetMetricColorToken());
        }
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return $"{GetMetricDisplayName()} Over Time";
    }

    private TimeSpan GetTimeSpan()
    {
        return Widget.Configuration.TimeRange switch
        {
            "15 Minutes" => TimeSpan.FromMinutes(15),
            "1 Hour" => TimeSpan.FromHours(1),
            "6 Hours" => TimeSpan.FromHours(6),
            "24 Hours" => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(1)
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _metricsService.MetricsBatchRecorded -= OnMetricsBatchRecorded;
        }
        base.Dispose(disposing);
    }
}
