namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.ServiceBus;

public partial class LiveStreamViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ILiveStreamService _liveStreamService;
    private readonly Func<IServiceBusOperations?> _getOperations;
    private IDisposable? _messageSubscription;
    private const int MaxMessages = 500;
    private const int FlushDelayMilliseconds = 100;
    private readonly object _messageLock = new();
    private readonly List<LiveStreamMessage> _pendingMessages = [];
    private readonly List<LiveStreamMessage> _messageBuffer = [];
    private int _isFlushScheduled;

    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private bool _isPeekMode = true;
    [ObservableProperty] private string? _currentEntityName;
    [ObservableProperty] private string? _currentEntityType;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private int _messageCount;
    [ObservableProperty] private LiveStreamMessage? _selectedMessage;
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private string _filterText = "";

    [ObservableProperty] private object? _selectedStreamEntity;
    [ObservableProperty] private bool _hasEntities;

    public ObservableCollection<LiveStreamMessage> Messages { get; } = [];
    public ObservableCollection<LiveStreamMessage> FilteredMessages { get; } = [];

    // Available entities for streaming
    public ObservableCollection<QueueInfo> AvailableQueues { get; } = [];
    public ObservableCollection<TopicInfo> AvailableTopics { get; } = [];

    public LiveStreamViewModel(
        ILiveStreamService liveStreamService,
        Func<IServiceBusOperations?> getOperations)
    {
        _liveStreamService = liveStreamService;
        _getOperations = getOperations;
        _liveStreamService.StreamingStatusChanged += OnStreamingStatusChanged;
        _liveStreamService.StreamError += OnStreamError;

        // Subscribe to message stream
        _messageSubscription = _liveStreamService.Messages
            .ObserveOn(System.Reactive.Concurrency.Scheduler.Default)
            .Subscribe(OnMessageReceived);
    }

    private void OnStreamingStatusChanged(object? sender, bool isStreaming)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsStreaming = isStreaming;
            IsConnecting = false;
        });
    }

    private void OnStreamError(object? sender, Exception ex)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ErrorMessage = ex.Message;
            IsConnecting = false;
        });
    }

    private void OnMessageReceived(LiveStreamMessage message)
    {
        lock (_messageLock)
        {
            _pendingMessages.Add(message);
        }

        ScheduleFlush();
    }

    partial void OnFilterTextChanged(string value)
    {
        _ = value; // Suppress unused warning
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        LiveStreamMessage[] snapshot;
        lock (_messageLock)
        {
            snapshot = _messageBuffer.ToArray();
        }

        FilteredMessages.Clear();
        for (var i = snapshot.Length - 1; i >= 0; i--)
        {
            var message = snapshot[i];
            if (MatchesFilter(message))
            {
                FilteredMessages.Add(message);
            }
        }
    }

    private bool MatchesFilter(LiveStreamMessage message)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        return message.MessageId.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
               message.Body.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
               (message.CorrelationId?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (message.SessionId?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [RelayCommand]
    private async Task StartQueueStreamAsync(string queueName)
    {
        try
        {
            var operations = _getOperations();
            if (operations == null)
            {
                ErrorMessage = "No active connection for live stream";
                IsConnecting = false;
                return;
            }

            ErrorMessage = null;
            IsConnecting = true;
            CurrentEntityName = queueName;
            CurrentEntityType = "Queue";

            await _liveStreamService.StartQueueStreamAsync(operations, queueName, IsPeekMode);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task StartSubscriptionStreamAsync((string topicName, string subscriptionName) args)
    {
        try
        {
            var operations = _getOperations();
            if (operations == null)
            {
                ErrorMessage = "No active connection for live stream";
                IsConnecting = false;
                return;
            }

            ErrorMessage = null;
            IsConnecting = true;
            CurrentEntityName = $"{args.topicName}/{args.subscriptionName}";
            CurrentEntityType = "Subscription";

            await _liveStreamService.StartSubscriptionStreamAsync(operations, args.topicName, args.subscriptionName, IsPeekMode);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsConnecting = false;
        }
    }

    /// <summary>
    /// Start streaming from a queue - public helper method
    /// </summary>
    public Task StartQueueAsync(string queueName)
    {
        return StartQueueStreamAsync(queueName);
    }

    /// <summary>
    /// Start streaming from a subscription - public helper method
    /// </summary>
    public Task StartSubscriptionAsync(string topicName, string subscriptionName)
    {
        return StartSubscriptionStreamAsync((topicName, subscriptionName));
    }

    [RelayCommand]
    private async Task StopStreamAsync()
    {
        await _liveStreamService.StopStreamAsync();
        CurrentEntityName = null;
        CurrentEntityType = null;
    }

    [RelayCommand]
    private void ClearMessages()
    {
        lock (_messageLock)
        {
            _pendingMessages.Clear();
            _messageBuffer.Clear();
        }

        Messages.Clear();
        FilteredMessages.Clear();
        MessageCount = 0;
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = "";
    }

    /// <summary>
    /// Initialize available entities for streaming selection
    /// </summary>
    public void SetAvailableEntities(IEnumerable<QueueInfo> queues, IEnumerable<TopicInfo> topics)
    {
        AvailableQueues.Clear();
        foreach (var queue in queues)
        {
            AvailableQueues.Add(queue);
        }

        AvailableTopics.Clear();
        foreach (var topic in topics)
        {
            AvailableTopics.Add(topic);
        }

        HasEntities = AvailableQueues.Count > 0 || AvailableTopics.Count > 0;
    }

    [RelayCommand]
    private async Task StartStreamForSelectedEntity()
    {
        if (SelectedStreamEntity == null)
            return;

        switch (SelectedStreamEntity)
        {
            case QueueInfo queue:
                await StartQueueAsync(queue.Name);
                break;
            case SubscriptionInfo subscription:
                await StartSubscriptionAsync(subscription.TopicName, subscription.Name);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _messageSubscription?.Dispose();
        _messageSubscription = null;
        _liveStreamService.StreamingStatusChanged -= OnStreamingStatusChanged;
        _liveStreamService.StreamError -= OnStreamError;

        // Only stop the stream, don't dispose the singleton service
        await _liveStreamService.StopStreamAsync();

        GC.SuppressFinalize(this);
    }

    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _isFlushScheduled, 1) == 1)
            return;

        _ = FlushPendingMessagesAsync();
    }

    private async Task FlushPendingMessagesAsync()
    {
        await Task.Delay(FlushDelayMilliseconds);
        Avalonia.Threading.Dispatcher.UIThread.Post(FlushPendingMessages);
    }

    private void FlushPendingMessages()
    {
        LiveStreamMessage[] snapshot;

        lock (_messageLock)
        {
            if (_pendingMessages.Count > 0)
            {
                _messageBuffer.AddRange(_pendingMessages);
                _pendingMessages.Clear();

                var overflow = _messageBuffer.Count - MaxMessages;
                if (overflow > 0)
                {
                    _messageBuffer.RemoveRange(0, overflow);
                }
            }

            snapshot = _messageBuffer.ToArray();
        }

        Messages.Clear();
        FilteredMessages.Clear();

        for (var i = snapshot.Length - 1; i >= 0; i--)
        {
            var message = snapshot[i];
            Messages.Add(message);
            if (MatchesFilter(message))
            {
                FilteredMessages.Add(message);
            }
        }

        MessageCount = snapshot.Length;
        Interlocked.Exchange(ref _isFlushScheduled, 0);

        lock (_messageLock)
        {
            if (_pendingMessages.Count > 0)
            {
                ScheduleFlush();
            }
        }
    }
}
