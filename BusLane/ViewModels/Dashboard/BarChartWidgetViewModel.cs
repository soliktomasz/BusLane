namespace BusLane.ViewModels.Dashboard;

using System;
using System.Collections.Generic;
using System.Linq;
using BusLane.Models;
using BusLane.Models.Dashboard;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class BarChartWidgetViewModel : DashboardWidgetViewModel
{
    private readonly List<QueueInfo> _queues = [];
    private readonly List<SubscriptionInfo> _subscriptions = [];

    [ObservableProperty]
    private BarPlotData? _plotData;

    public BarChartWidgetViewModel(DashboardWidget widget) : base(widget)
    {
        RefreshData();
    }

    public void UpdateEntityData(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        _queues.Clear();
        _queues.AddRange(queues);

        _subscriptions.Clear();
        _subscriptions.AddRange(subscriptions);

        RefreshData();
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();

            var topCount = Widget.Configuration.TopEntities <= 0 ? 10 : Widget.Configuration.TopEntities;
            var entities = _queues.Select(q => (Name: q.Name, Active: (double)GetPrimaryMetric(q), DeadLetter: (double)GetSecondaryMetric(q)))
                .Concat(_subscriptions.Select(s => (Name: $"{s.TopicName}/{s.Name}", Active: (double)GetPrimaryMetric(s), DeadLetter: (double)GetSecondaryMetric(s))))
                .OrderByDescending(e => e.Active + e.DeadLetter)
                .Take(topCount)
                .ToList();

            var labels = entities.Select(e => ShortLabel(e.Name)).ToArray();
            var activeValues = entities.Select(e => e.Active).ToArray();
            var deadValues = entities.Select(e => e.DeadLetter).ToArray();

            var series = new List<BarPlotSeries>
            {
                new(GetPrimaryMetricName(), activeValues, GetMetricColorToken())
            };

            if (Widget.Configuration.ShowSecondaryMetric)
            {
                series.Add(new BarPlotSeries(GetSecondaryMetricName(), deadValues, "TextDanger"));
            }

            PlotData = new BarPlotData(Title, labels, series);
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

    private long GetPrimaryMetric(QueueInfo queue) => GetPrimaryMetricValue(queue);

    private long GetSecondaryMetric(QueueInfo queue) => GetSecondaryMetricValue(queue);

    private long GetPrimaryMetric(SubscriptionInfo sub) => GetPrimaryMetricValue(sub);

    private long GetSecondaryMetric(SubscriptionInfo sub) => GetSecondaryMetricValue(sub);

    private string GetPrimaryMetricName() => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => "Active Messages",
        "DeadLetterCount" => "Dead Letters",
        _ => "Active Messages"
    };

    private string GetSecondaryMetricName() => "Dead Letters";

    private static string ShortLabel(string name)
    {
        if (name.Length <= 14)
        {
            return name;
        }

        return name[..13] + "\u2026";
    }
}
