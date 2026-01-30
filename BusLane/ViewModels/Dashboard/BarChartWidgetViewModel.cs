namespace BusLane.ViewModels.Dashboard;

using System.Collections.ObjectModel;
using BusLane.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

public partial class BarChartWidgetViewModel : DashboardWidgetViewModel
{
    public ObservableCollection<ISeries> Series { get; } = [];
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    private readonly ObservableCollection<QueueInfo> _queues = [];
    private readonly ObservableCollection<SubscriptionInfo> _subscriptions = [];

    public BarChartWidgetViewModel(DashboardWidget widget) : base(widget)
    {
        XAxes = [new Axis
        {
            Name = "Entity",
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            Labels = []
        }];

        YAxes = [new Axis
        {
            Name = "Count",
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }];

        RefreshData();
    }

    public void UpdateEntityData(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        _queues.Clear();
        foreach (var q in queues)
            _queues.Add(q);

        _subscriptions.Clear();
        foreach (var s in subscriptions)
            _subscriptions.Add(s);

        RefreshData();
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();
            Series.Clear();

            var entities = _queues.Select(q => (Name: q.Name, Active: (double)GetPrimaryMetric(q), DeadLetter: (double)GetSecondaryMetric(q)))
                .Concat(_subscriptions.Select(s => (Name: $"{s.TopicName}/{s.Name}", Active: (double)GetPrimaryMetric(s), DeadLetter: (double)GetSecondaryMetric(s))))
                .OrderByDescending(e => e.Active + e.DeadLetter)
                .Take(Widget.Configuration.TopEntities)
                .ToList();

            XAxes[0].Labels = entities.Select(e => e.Name).ToArray();

            Series.Add(new ColumnSeries<double>
            {
                Name = GetPrimaryMetricName(),
                Values = entities.Select(e => e.Active).ToList(),
                Fill = new SolidColorPaint(SKColors.DodgerBlue)
            });

            if (Widget.Configuration.ShowSecondaryMetric)
            {
                Series.Add(new ColumnSeries<double>
                {
                    Name = GetSecondaryMetricName(),
                    Values = entities.Select(e => e.DeadLetter).ToList(),
                    Fill = new SolidColorPaint(SKColors.OrangeRed)
                });
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return "Entity Comparison";
    }

    private long GetPrimaryMetric(QueueInfo queue) => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => queue.ActiveMessageCount,
        "DeadLetterCount" => queue.DeadLetterCount,
        _ => queue.ActiveMessageCount
    };

    private long GetSecondaryMetric(QueueInfo queue) => queue.DeadLetterCount;

    private long GetPrimaryMetric(SubscriptionInfo sub) => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => sub.ActiveMessageCount,
        "DeadLetterCount" => sub.DeadLetterCount,
        _ => sub.ActiveMessageCount
    };

    private long GetSecondaryMetric(SubscriptionInfo sub) => sub.DeadLetterCount;

    private string GetPrimaryMetricName() => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => "Active Messages",
        "DeadLetterCount" => "Dead Letters",
        _ => "Active Messages"
    };

    private string GetSecondaryMetricName() => "Dead Letters";
}
