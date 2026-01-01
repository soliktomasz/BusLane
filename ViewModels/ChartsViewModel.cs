using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace BusLane.ViewModels;

using Services.Monitoring;

public partial class ChartsViewModel : ViewModelBase
{
    private readonly IMetricsService _metricsService;

    [ObservableProperty] private string _selectedTimeRange = "1 Hour";
    [ObservableProperty] private string? _selectedEntityName;
    [ObservableProperty] private bool _showAggregated = true;

    public string[] AvailableTimeRanges { get; } = ["15 Minutes", "1 Hour", "6 Hours", "24 Hours"];

    // Message count over time chart
    public ObservableCollection<ISeries> MessageCountSeries { get; } = [];
    public Axis[] MessageCountXAxes { get; }
    public Axis[] MessageCountYAxes { get; }

    // Dead letter count over time chart
    public ObservableCollection<ISeries> DeadLetterSeries { get; } = [];
    public Axis[] DeadLetterXAxes { get; }
    public Axis[] DeadLetterYAxes { get; }

    // Current entity distribution (pie chart)
    public ObservableCollection<ISeries> EntityDistributionSeries { get; } = [];

    // Queue/Subscription comparison (bar chart)
    public ObservableCollection<ISeries> ComparisonSeries { get; } = [];
    public Axis[] ComparisonXAxes { get; }
    public Axis[] ComparisonYAxes { get; }

    public ChartsViewModel(IMetricsService metricsService)
    {
        _metricsService = metricsService;
        _metricsService.MetricRecorded += OnMetricRecorded;

        // Initialize axes - DateTimePoint uses DateTime.Ticks as X value
        MessageCountXAxes = [new Axis
        {
            Name = "Time",
            Labeler = value =>
            {
                try
                {
                    return new DateTime((long)value).ToString("HH:mm");
                }
                catch
                {
                    return "";
                }
            },
            TextSize = 12,
            NameTextSize = 14,
            LabelsPaint = new SolidColorPaint(SKColors.Gray)
        }];
        MessageCountYAxes = [new Axis
        {
            Name = "Message Count",
            TextSize = 12,
            NameTextSize = 14,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }];

        DeadLetterXAxes = [new Axis
        {
            Name = "Time",
            Labeler = value =>
            {
                try
                {
                    return new DateTime((long)value).ToString("HH:mm");
                }
                catch
                {
                    return "";
                }
            },
            TextSize = 12,
            NameTextSize = 14,
            LabelsPaint = new SolidColorPaint(SKColors.Gray)
        }];
        DeadLetterYAxes = [new Axis
        {
            Name = "Dead Letter Count",
            TextSize = 12,
            NameTextSize = 14,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }];

        ComparisonXAxes = [new Axis
        {
            Name = "Entity",
            TextSize = 12,
            NameTextSize = 14,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            Labels = []
        }];
        ComparisonYAxes = [new Axis
        {
            Name = "Count",
            TextSize = 12,
            NameTextSize = 14,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }];

        // Initialize with empty data
        InitializeCharts();
    }

    private void InitializeCharts()
    {
        // Message count line chart
        MessageCountSeries.Add(new LineSeries<DateTimePoint>
        {
            Name = "Active Messages",
            Values = new ObservableCollection<DateTimePoint>(),
            Fill = null,
            GeometrySize = 6,
            Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
            GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue, 2)
        });

        // Dead letter line chart
        DeadLetterSeries.Add(new LineSeries<DateTimePoint>
        {
            Name = "Dead Letters",
            Values = new ObservableCollection<DateTimePoint>(),
            Fill = null,
            GeometrySize = 6,
            Stroke = new SolidColorPaint(SKColors.OrangeRed, 2),
            GeometryStroke = new SolidColorPaint(SKColors.OrangeRed, 2)
        });
    }

    private void OnMetricRecorded(object? sender, MetricDataPoint dataPoint)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshCharts();
        });
    }

    partial void OnSelectedTimeRangeChanged(string value)
    {
        RefreshCharts();
    }

    partial void OnSelectedEntityNameChanged(string? value)
    {
        RefreshCharts();
    }

    partial void OnShowAggregatedChanged(bool value)
    {
        RefreshCharts();
    }

    private TimeSpan GetTimeSpan()
    {
        return SelectedTimeRange switch
        {
            "15 Minutes" => TimeSpan.FromMinutes(15),
            "1 Hour" => TimeSpan.FromHours(1),
            "6 Hours" => TimeSpan.FromHours(6),
            "24 Hours" => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(1)
        };
    }

    [RelayCommand]
    public void RefreshCharts()
    {
        var duration = GetTimeSpan();

        // Update message count chart
        UpdateMessageCountChart(duration);

        // Update dead letter chart
        UpdateDeadLetterChart(duration);
    }

    private void UpdateMessageCountChart(TimeSpan duration)
    {
        IEnumerable<MetricDataPoint> metrics;

        if (ShowAggregated || string.IsNullOrEmpty(SelectedEntityName))
        {
            metrics = _metricsService.GetAggregatedMetrics("ActiveMessageCount", duration);
        }
        else
        {
            metrics = _metricsService.GetMetricHistory(SelectedEntityName, "ActiveMessageCount", duration);
        }

        var points = metrics
            .GroupBy(m => new DateTime(m.Timestamp.Year, m.Timestamp.Month, m.Timestamp.Day,
                m.Timestamp.Hour, m.Timestamp.Minute / 5 * 5, 0)) // Group by 5-minute intervals
            .Select(g => new DateTimePoint(g.Key, g.Sum(m => m.Value)))
            .OrderBy(p => p.DateTime)
            .ToList();

        if (MessageCountSeries.Count > 0 && MessageCountSeries[0] is LineSeries<DateTimePoint> series)
        {
            var values = (ObservableCollection<DateTimePoint>)series.Values!;
            values.Clear();
            foreach (var point in points)
            {
                values.Add(point);
            }
        }
    }

    private void UpdateDeadLetterChart(TimeSpan duration)
    {
        IEnumerable<MetricDataPoint> metrics;

        if (ShowAggregated || string.IsNullOrEmpty(SelectedEntityName))
        {
            metrics = _metricsService.GetAggregatedMetrics("DeadLetterCount", duration);
        }
        else
        {
            metrics = _metricsService.GetMetricHistory(SelectedEntityName, "DeadLetterCount", duration);
        }

        var points = metrics
            .GroupBy(m => new DateTime(m.Timestamp.Year, m.Timestamp.Month, m.Timestamp.Day,
                m.Timestamp.Hour, m.Timestamp.Minute / 5 * 5, 0))
            .Select(g => new DateTimePoint(g.Key, g.Sum(m => m.Value)))
            .OrderBy(p => p.DateTime)
            .ToList();

        if (DeadLetterSeries.Count > 0 && DeadLetterSeries[0] is LineSeries<DateTimePoint> series)
        {
            var values = (ObservableCollection<DateTimePoint>)series.Values!;
            values.Clear();
            foreach (var point in points)
            {
                values.Add(point);
            }
        }
    }

    public void UpdateEntityDistribution(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        EntityDistributionSeries.Clear();

        var data = new List<(string Name, double Value)>();

        foreach (var queue in queues.OrderByDescending(q => q.ActiveMessageCount).Take(10))
        {
            data.Add((queue.Name, queue.ActiveMessageCount));
        }

        foreach (var sub in subscriptions.OrderByDescending(s => s.ActiveMessageCount).Take(10))
        {
            data.Add(($"{sub.TopicName}/{sub.Name}", sub.ActiveMessageCount));
        }

        var colors = new[]
        {
            SKColors.DodgerBlue, SKColors.Orange, SKColors.Green, SKColors.Purple,
            SKColors.Red, SKColors.Teal, SKColors.Gold, SKColors.Pink,
            SKColors.LimeGreen, SKColors.Coral
        };

        for (var i = 0; i < data.Count && i < colors.Length; i++)
        {
            EntityDistributionSeries.Add(new PieSeries<double>
            {
                Name = data[i].Name,
                Values = [data[i].Value],
                Fill = new SolidColorPaint(colors[i]),
                DataLabelsSize = 12,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
            });
        }
    }

    public void UpdateComparisonChart(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        ComparisonSeries.Clear();

        var entities = queues.Select(q => (Name: q.Name, Active: (double)q.ActiveMessageCount, DeadLetter: (double)q.DeadLetterCount))
            .Concat(subscriptions.Select(s => (Name: $"{s.TopicName}/{s.Name}", Active: (double)s.ActiveMessageCount, DeadLetter: (double)s.DeadLetterCount)))
            .OrderByDescending(e => e.Active + e.DeadLetter)
            .Take(10)
            .ToList();

        ComparisonXAxes[0].Labels = entities.Select(e => e.Name).ToArray();

        ComparisonSeries.Add(new ColumnSeries<double>
        {
            Name = "Active Messages",
            Values = entities.Select(e => e.Active).ToList(),
            Fill = new SolidColorPaint(SKColors.DodgerBlue)
        });

        ComparisonSeries.Add(new ColumnSeries<double>
        {
            Name = "Dead Letters",
            Values = entities.Select(e => e.DeadLetter).ToList(),
            Fill = new SolidColorPaint(SKColors.OrangeRed)
        });
    }

    public void RecordCurrentMetrics(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        foreach (var queue in queues)
        {
            _metricsService.RecordMetric(queue.Name, "ActiveMessageCount", queue.ActiveMessageCount);
            _metricsService.RecordMetric(queue.Name, "DeadLetterCount", queue.DeadLetterCount);
            _metricsService.RecordMetric(queue.Name, "ScheduledCount", queue.ScheduledCount);
            _metricsService.RecordMetric(queue.Name, "SizeInBytes", queue.SizeInBytes);
        }

        foreach (var sub in subscriptions)
        {
            var entityName = $"{sub.TopicName}/{sub.Name}";
            _metricsService.RecordMetric(entityName, "ActiveMessageCount", sub.ActiveMessageCount);
            _metricsService.RecordMetric(entityName, "DeadLetterCount", sub.DeadLetterCount);
        }
    }
}

