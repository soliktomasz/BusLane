namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Manages feature panel visibility and lifecycle for auxiliary operator tools.
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
    private readonly ICorrelationMessageCatalog? _correlationCatalog;
    private readonly Func<CorrelationSourceContext?>? _getCorrelationContext;
    private readonly IMessageReplayService? _messageReplayService;
    private readonly IReplayAuditStore? _replayAuditStore;
    private readonly Func<IReadOnlyList<ReplayDestination>>? _getReplayDestinations;
    private readonly Func<BusLane.Services.Abstractions.IFileDialogService?>? _getFileDialogService;
    private readonly ICorrelationRefreshDelay? _correlationRefreshDelay;
    private readonly ICorrelationMessageComparisonService? _correlationComparisonService;
    private readonly IScheduledMessageManagementService? _scheduledMessageManagementService;
    private readonly Func<ScheduledMessageResolvedEntry, ScheduledMessagePayload, Task>? _cloneScheduledMessage;
    private readonly TimeProvider _timeProvider;

    [ObservableProperty] private bool _showLiveStream;
    [ObservableProperty] private bool _showAlerts;
    [ObservableProperty] private bool _showCorrelationExplorer;
    [ObservableProperty] private bool _showScheduledMessages;
    [ObservableProperty] private LiveStreamViewModel? _liveStreamViewModel;
    [ObservableProperty] private AlertsViewModel? _alertsViewModel;
    [ObservableProperty] private CorrelationExplorerViewModel? _correlationExplorerViewModel;
    [ObservableProperty] private ScheduledMessagesViewModel? _scheduledMessagesViewModel;
    [ObservableProperty] private int _activeAlertCount;

    public FeaturePanelsViewModel(
        ILiveStreamService liveStreamService,
        IAlertService alertService,
        INotificationService notificationService,
        Func<IServiceBusOperations?> getOperations,
        Func<ObservableCollection<QueueInfo>> getQueues,
        Func<ObservableCollection<TopicInfo>> getTopics,
        Func<ObservableCollection<SubscriptionInfo>> getSubscriptions,
        Func<QueueInfo?> getSelectedQueue,
        Func<SubscriptionInfo?> getSelectedSubscription,
        Action<string> setStatus,
        ICorrelationMessageCatalog? correlationCatalog = null,
        Func<CorrelationSourceContext?>? getCorrelationContext = null,
        IMessageReplayService? messageReplayService = null,
        IReplayAuditStore? replayAuditStore = null,
        Func<IReadOnlyList<ReplayDestination>>? getReplayDestinations = null,
        Func<BusLane.Services.Abstractions.IFileDialogService?>? getFileDialogService = null,
        ICorrelationRefreshDelay? correlationRefreshDelay = null,
        ICorrelationMessageComparisonService? correlationComparisonService = null,
        IScheduledMessageManagementService? scheduledMessageManagementService = null,
        Func<ScheduledMessageResolvedEntry, ScheduledMessagePayload, Task>? cloneScheduledMessage = null,
        TimeProvider? timeProvider = null)
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
        _correlationCatalog = correlationCatalog;
        _getCorrelationContext = getCorrelationContext;
        _messageReplayService = messageReplayService;
        _replayAuditStore = replayAuditStore;
        _getReplayDestinations = getReplayDestinations;
        _getFileDialogService = getFileDialogService;
        _correlationRefreshDelay = correlationRefreshDelay;
        _correlationComparisonService = correlationComparisonService;
        _scheduledMessageManagementService = scheduledMessageManagementService;
        _cloneScheduledMessage = cloneScheduledMessage;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
        CloseScheduledMessages();
        DisposeCorrelationExplorer();
        LiveStreamViewModel = new LiveStreamViewModel(
            _liveStreamService,
            _getOperations,
            _correlationCatalog,
            _getCorrelationContext);
        LiveStreamViewModel.SetAvailableEntities(_getQueues(), _getTopics());

        ShowLiveStream = true;
        ShowAlerts = false;
        ShowCorrelationExplorer = false;

        await StartLiveStreamForSelectedEntity();
    }

    public void CloseLiveStream()
    {
        ShowLiveStream = false;
        _ = LiveStreamViewModel?.DisposeAsync();
        LiveStreamViewModel = null;
    }

    public void OpenAlerts()
    {
        CloseScheduledMessages();
        DisposeCorrelationExplorer();
        AlertsViewModel = new AlertsViewModel(_alertService, _notificationService, () => ShowAlerts = false);
        ShowAlerts = true;
        ShowLiveStream = false;
    }

    public void CloseAlerts()
    {
        ShowAlerts = false;
        AlertsViewModel = null;
    }

    public async Task OpenCorrelationExplorer()
    {
        CloseScheduledMessages();
        if (_correlationCatalog == null ||
            _messageReplayService == null ||
            _replayAuditStore == null ||
            _getReplayDestinations == null)
        {
            _setStatus("Correlation Explorer is not available");
            return;
        }

        CorrelationExplorerViewModel?.Dispose();
        CorrelationExplorerViewModel = new CorrelationExplorerViewModel(
            _correlationCatalog,
            _replayAuditStore,
            _messageReplayService,
            _getOperations,
            _getReplayDestinations,
            _getFileDialogService?.Invoke(),
            refreshDelay: _correlationRefreshDelay,
            comparisonService: _correlationComparisonService);
        await CorrelationExplorerViewModel.RefreshAsync();

        ShowCorrelationExplorer = true;
        ShowLiveStream = false;
        ShowAlerts = false;
    }

    public void CloseCorrelationExplorer()
    {
        DisposeCorrelationExplorer();
    }

    public async Task OpenScheduledMessages()
    {
        if (_scheduledMessageManagementService is null || _cloneScheduledMessage is null)
        {
            _setStatus("Scheduled Messages is not available");
            return;
        }

        CloseLiveStream();
        CloseAlerts();
        CloseCorrelationExplorer();
        ScheduledMessagesViewModel = new ScheduledMessagesViewModel(
            _scheduledMessageManagementService,
            _cloneScheduledMessage,
            _timeProvider);
        await ScheduledMessagesViewModel.RefreshCommand.ExecuteAsync(null);
        ShowScheduledMessages = true;
    }

    public void CloseScheduledMessages()
    {
        ShowScheduledMessages = false;
        ScheduledMessagesViewModel = null;
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
        CloseAlerts();
        CloseCorrelationExplorer();
        CloseScheduledMessages();
    }

    private void DisposeCorrelationExplorer()
    {
        ShowCorrelationExplorer = false;
        CorrelationExplorerViewModel?.Dispose();
        CorrelationExplorerViewModel = null;
    }
}
