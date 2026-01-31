namespace BusLane.ViewModels.Dashboard;

using System.Collections.ObjectModel;
using BusLane.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

public partial class PieChartWidgetViewModel : DashboardWidgetViewModel
{
    public ObservableCollection<ISeries> Series { get; } = [];

    private readonly ObservableCollection<QueueInfo> _queues = [];
    private readonly ObservableCollection<SubscriptionInfo> _subscriptions = [];

    public PieChartWidgetViewModel(DashboardWidget widget) : base(widget)
    {
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

            var data = _queues.Select(q => (Name: q.Name, Value: GetMetricValue(q)))
                .Concat(_subscriptions.Select(s => (Name: $"{s.TopicName}/{s.Name}", Value: GetMetricValue(s))))
                .OrderByDescending(e => e.Value)
                .Take(Widget.Configuration.TopEntities)
                .ToList();

            var colors = new[]
            {
                SKColors.DodgerBlue, SKColors.Orange, SKColors.Green, SKColors.Purple,
                SKColors.Red, SKColors.Teal, SKColors.Gold, SKColors.Pink,
                SKColors.LimeGreen, SKColors.Coral
            };

            for (var i = 0; i < data.Count && i < colors.Length; i++)
            {
                Series.Add(new PieSeries<double>
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
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return $"{GetMetricDisplayName()} Distribution";
    }

    private double GetMetricValue(QueueInfo queue)
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => queue.ActiveMessageCount,
            "DeadLetterCount" => queue.DeadLetterCount,
            _ => queue.ActiveMessageCount
        };
    }

    private double GetMetricValue(SubscriptionInfo sub)
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => sub.ActiveMessageCount,
            "DeadLetterCount" => sub.DeadLetterCount,
            _ => sub.ActiveMessageCount
        };
    }

    private string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Message",
            "DeadLetterCount" => "Dead Letter",
            _ => "Message"
        };
    }
}
