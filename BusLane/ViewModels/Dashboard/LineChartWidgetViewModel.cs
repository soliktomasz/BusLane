namespace BusLane.ViewModels.Dashboard;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Monitoring;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

public partial class LineChartWidgetViewModel : DashboardWidgetViewModel
{
    private readonly IMetricsService _metricsService;

    public ObservableCollection<ISeries> Series { get; } = [];
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public LineChartWidgetViewModel(DashboardWidget widget, IMetricsService metricsService) : base(widget)
    {
        _metricsService = metricsService;

        XAxes = [new Axis
        {
            Name = "Time",
            Labeler = value =>
            {
                try { return new DateTime((long)value).ToString("HH:mm"); }
                catch { return ""; }
            },
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray)
        }];

        YAxes = [new Axis
        {
            Name = GetMetricDisplayName(),
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }];

        Series.Add(new LineSeries<DateTimePoint>
        {
            Name = GetMetricDisplayName(),
            Values = new ObservableCollection<DateTimePoint>(),
            Fill = null,
            GeometrySize = 4,
            Stroke = new SolidColorPaint(GetMetricColor(), 2),
            GeometryStroke = new SolidColorPaint(GetMetricColor(), 2)
        });

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
                .Select(g => new DateTimePoint(g.Key, g.Sum(m => m.Value)))
                .OrderBy(p => p.DateTime)
                .ToList();

            if (Series.Count > 0 && Series[0] is LineSeries<DateTimePoint> series)
            {
                var values = (ObservableCollection<DateTimePoint>)series.Values!;
                values.Clear();
                foreach (var point in points)
                {
                    values.Add(point);
                }
            }
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

    private string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Active Messages",
            "DeadLetterCount" => "Dead Letters",
            "ScheduledCount" => "Scheduled Messages",
            "SizeInBytes" => "Size (Bytes)",
            _ => Widget.Configuration.MetricName
        };
    }

    private SKColor GetMetricColor()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => SKColors.DodgerBlue,
            "DeadLetterCount" => SKColors.OrangeRed,
            "ScheduledCount" => SKColors.Green,
            "SizeInBytes" => SKColors.Purple,
            _ => SKColors.DodgerBlue
        };
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
            _metricsService.MetricRecorded -= OnMetricRecorded;
        }
        base.Dispose(disposing);
    }
}
