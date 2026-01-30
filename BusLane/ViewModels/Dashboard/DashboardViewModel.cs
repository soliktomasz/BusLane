namespace BusLane.ViewModels.Dashboard;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Dashboard;
using BusLane.Services.Monitoring;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardPersistenceService _persistenceService;
    private readonly DashboardLayoutEngine _layoutEngine;
    private readonly IMetricsService _metricsService;

    [ObservableProperty] private ObservableCollection<DashboardWidgetViewModel> _widgets = [];
    [ObservableProperty] private bool _isAddWidgetDialogOpen;

    private const int MaxWidgets = 20;

    public bool CanAddWidget => Widgets.Count < MaxWidgets;

    public DashboardViewModel(
        IDashboardPersistenceService persistenceService,
        DashboardLayoutEngine layoutEngine,
        IMetricsService metricsService)
    {
        _persistenceService = persistenceService;
        _layoutEngine = layoutEngine;
        _metricsService = metricsService;

        LoadDashboard();
    }

    private void LoadDashboard()
    {
        var config = _persistenceService.Load();

        foreach (var widget in config.Widgets)
        {
            AddWidgetViewModel(widget);
        }
    }

    private void AddWidgetViewModel(DashboardWidget widget)
    {
        DashboardWidgetViewModel vm = widget.Type switch
        {
            WidgetType.LineChart => new LineChartWidgetViewModel(widget, _metricsService),
            WidgetType.PieChart => new PieChartWidgetViewModel(widget),
            WidgetType.BarChart => new BarChartWidgetViewModel(widget),
            WidgetType.MetricCard => new MetricCardWidgetViewModel(widget, _metricsService),
            _ => throw new NotSupportedException($"Widget type {widget.Type} not supported")
        };

        Widgets.Add(vm);
        OnPropertyChanged(nameof(CanAddWidget));
    }

    [RelayCommand]
    private void AddWidget(WidgetType type)
    {
        if (!CanAddWidget)
            return;

        var widgetModels = new ObservableCollection<DashboardWidget>(Widgets.Select(w => w.Widget));
        var (row, col) = _layoutEngine.FindNextAvailableSlot(widgetModels, 6, 4);

        var widget = new DashboardWidget
        {
            Type = type,
            Row = row,
            Column = col,
            Width = 6,
            Height = 4,
            Configuration = new WidgetConfiguration()
        };

        AddWidgetViewModel(widget);
        SaveDashboard();

        IsAddWidgetDialogOpen = false;
    }

    [RelayCommand]
    private void RemoveWidget(DashboardWidgetViewModel widget)
    {
        Widgets.Remove(widget);
        OnPropertyChanged(nameof(CanAddWidget));
        SaveDashboard();
    }

    [RelayCommand]
    private void OpenAddWidgetDialog()
    {
        IsAddWidgetDialogOpen = true;
    }

    [RelayCommand]
    private void CloseAddWidgetDialog()
    {
        IsAddWidgetDialogOpen = false;
    }

    public void UpdateEntityData(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        foreach (var widget in Widgets)
        {
            if (widget is PieChartWidgetViewModel pie)
                pie.UpdateEntityData(queues, subscriptions);
            else if (widget is BarChartWidgetViewModel bar)
                bar.UpdateEntityData(queues, subscriptions);
        }
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

    private void SaveDashboard()
    {
        var config = new DashboardConfiguration
        {
            Widgets = Widgets.Select(w => w.Widget).ToList()
        };
        _persistenceService.Save(config);
    }
}
