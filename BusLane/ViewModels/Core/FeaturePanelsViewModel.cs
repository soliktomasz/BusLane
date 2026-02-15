namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Dashboard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Manages feature panel visibility and lifecycle (Live Stream, Charts, Alerts).
/// Follows single responsibility - just panel orchestration.
/// </summary>
public partial class FeaturePanelsViewModel : ViewModelBase
{
    private readonly ILiveStreamService _liveStreamService;
    private readonly IAlertService _alertService;
    private readonly INotificationService _notificationService;
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly Func<ObservableCollection<QueueInfo>> _getQueues;
    private readonly Func<ObservableCollection<TopicInfo>> _getTopics;
    private readonly Func<ObservableCollection<SubscriptionInfo>> _getSubscriptions;
    private readonly Func<QueueInfo?> _getSelectedQueue;
    private readonly Func<SubscriptionInfo?> _getSelectedSubscription;
    private readonly Action<string> _setStatus;

    [ObservableProperty] private bool _showLiveStream;
    [ObservableProperty] private bool _showCharts;
    [ObservableProperty] private bool _showAlerts;
    [ObservableProperty] private LiveStreamViewModel? _liveStreamViewModel;
    [ObservableProperty] private DashboardViewModel? _dashboardViewModel;
    [ObservableProperty] private AlertsViewModel? _alertsViewModel;
    [ObservableProperty] private int _activeAlertCount;

    public FeaturePanelsViewModel(
        ILiveStreamService liveStreamService,
        IAlertService alertService,
        INotificationService notificationService,
        DashboardViewModel dashboardViewModel,
        Func<IServiceBusOperations?> getOperations,
        Func<ObservableCollection<QueueInfo>> getQueues,
        Func<ObservableCollection<TopicInfo>> getTopics,
        Func<ObservableCollection<SubscriptionInfo>> getSubscriptions,
        Func<QueueInfo?> getSelectedQueue,
        Func<SubscriptionInfo?> getSelectedSubscription,
        Action<string> setStatus)
    {
        _liveStreamService = liveStreamService;
        _alertService = alertService;
        _notificationService = notificationService;
        _getOperations = getOperations;
        _getQueues = getQueues;
        _getTopics = getTopics;
        _getSubscriptions = getSubscriptions;
        _getSelectedQueue = getSelectedQueue;
        _getSelectedSubscription = getSelectedSubscription;
        _setStatus = setStatus;
        DashboardViewModel = dashboardViewModel;

        _alertService.AlertTriggered += OnAlertTriggered;
        _alertService.AlertsChanged += OnAlertsChanged;
        ActiveAlertCount = _alertService.ActiveAlerts.Count(a => !a.IsAcknowledged);
    }

    private void OnAlertTriggered(object? sender, AlertEvent alert)
    {
        _notificationService.ShowAlertNotification(alert);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ActiveAlertCount = _alertService.ActiveAlerts.Count(a => !a.IsAcknowledged);
            _setStatus($"Alert: {alert.Rule.Name} - {alert.EntityName}");
        });
    }

    private void OnAlertsChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ActiveAlertCount = _alertService.ActiveAlerts.Count(a => !a.IsAcknowledged);
        });
    }

    [RelayCommand]
    public async Task OpenLiveStream()
    {
        LiveStreamViewModel = new LiveStreamViewModel(_liveStreamService, _getOperations);
        LiveStreamViewModel.SetAvailableEntities(_getQueues(), _getTopics());

        ShowLiveStream = true;
        ShowCharts = false;
        ShowAlerts = false;

        await StartLiveStreamForSelectedEntity();
    }

    public void CloseLiveStream()
    {
        ShowLiveStream = false;
        _ = LiveStreamViewModel?.DisposeAsync();
        LiveStreamViewModel = null;
    }

    public void OpenCharts()
    {
        var queues = _getQueues();
        var subscriptions = _getSubscriptions();

        DashboardViewModel?.UpdateEntityData(queues, subscriptions);
        DashboardViewModel?.RecordCurrentMetrics(queues, subscriptions);

        ShowCharts = true;
        ShowLiveStream = false;
        ShowAlerts = false;
    }

    public void CloseCharts()
    {
        ShowCharts = false;
    }

    public void OpenAlerts()
    {
        AlertsViewModel = new AlertsViewModel(_alertService, _notificationService, () => ShowAlerts = false);
        ShowAlerts = true;
        ShowLiveStream = false;
        ShowCharts = false;
    }

    public void CloseAlerts()
    {
        ShowAlerts = false;
        AlertsViewModel = null;
    }

    [RelayCommand]
    public async Task StartLiveStreamForSelectedEntity()
    {
        if (LiveStreamViewModel == null) return;

        var selectedQueue = _getSelectedQueue();
        var selectedSubscription = _getSelectedSubscription();

        if (selectedQueue != null)
        {
            await LiveStreamViewModel.StartQueueAsync(selectedQueue.Name);
        }
        else if (selectedSubscription != null)
        {
            await LiveStreamViewModel.StartSubscriptionAsync(selectedSubscription.TopicName, selectedSubscription.Name);
        }
    }

    [RelayCommand]
    public async Task EvaluateAlerts()
    {
        await _alertService.EvaluateAlertsAsync(_getQueues(), _getSubscriptions());
    }

    /// <summary>
    /// Closes all open panels.
    /// </summary>
    public void CloseAll()
    {
        CloseLiveStream();
        CloseCharts();
        CloseAlerts();
    }
}
