namespace BusLane.ViewModels.Dashboard;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BusLane.Models;
using BusLane.Models.Dashboard;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class PieChartWidgetViewModel : DashboardWidgetViewModel
{
    [ObservableProperty]
    private PiePlotData? _plotData;

    private readonly ObservableCollection<QueueInfo> _queues = [];
    private readonly ObservableCollection<SubscriptionInfo> _subscriptions = [];

    private static readonly string[] Palette =
    [
        "PaletteChart1",
        "PaletteChart2",
        "PaletteChart3",
        "PaletteChart4",
        "PaletteChart5",
        "PaletteChart6"
    ];

    public PieChartWidgetViewModel(DashboardWidget widget) : base(widget)
    {
        RefreshData();
    }

    public void UpdateEntityData(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        _queues.Clear();
        foreach (var q in queues)
        {
            _queues.Add(q);
        }

        _subscriptions.Clear();
        foreach (var s in subscriptions)
        {
            _subscriptions.Add(s);
        }

        RefreshData();
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();

            var data = _queues.Select(q => (Name: q.Name, Value: GetMetricValue(q)))
                .Concat(_subscriptions.Select(s => (Name: $"{s.TopicName}/{s.Name}", Value: GetMetricValue(s))))
                .Where(e => e.Value > 0)
                .OrderByDescending(e => e.Value)
                .ToList();

            var topCount = Widget.Configuration.TopEntities <= 0 ? 10 : Widget.Configuration.TopEntities;
            var top = data.Take(topCount).ToList();
            var slices = new List<PiePlotSlice>();

            for (var i = 0; i < top.Count; i++)
            {
                slices.Add(new PiePlotSlice(top[i].Name, top[i].Value, Palette[i % Palette.Length]));
            }

            if (top.Count < data.Count)
            {
                var rest = data.Skip(top.Count).Sum(e => e.Value);
                slices.Add(new PiePlotSlice("Other", rest, "SubtleForeground"));
            }

            PlotData = new PiePlotData(Title, slices);
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

    private double GetMetricValue(QueueInfo queue) => GetPrimaryMetricValue(queue);

    private double GetMetricValue(SubscriptionInfo sub) => GetPrimaryMetricValue(sub);

    private new string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Message",
            "DeadLetterCount" => "Dead Letter",
            _ => "Message"
        };
    }
}
