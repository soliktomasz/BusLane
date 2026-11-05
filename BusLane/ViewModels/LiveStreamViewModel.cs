namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Input.Platform;
using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.ServiceBus;

public partial class LiveStreamViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ILiveStreamService _liveStreamService;
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly ICorrelationMessageCatalog? _correlationCatalog;
    private readonly Func<CorrelationSourceContext?>? _getCorrelationContext;
    private IDisposable? _messageSubscription;
    private const int MaxMessages = 500;
    private const int FlushDelayMilliseconds = 100;
    private readonly object _messageLock = new();
    private readonly List<LiveStreamMessage> _pendingMessages = [];
    private readonly List<LiveStreamMessage> _messageBuffer = [];
    private int _isFlushScheduled;
    // Guarded by _messageLock. Resuming peeks the entity from its start, so already-seen sequence numbers are dropped.
    private long _lastSequenceNumber;
    private Func<Task>? _restartStream;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLive), nameof(IsStopped), nameof(IsActive))]
    private bool _isStreaming;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEntitySelector), nameof(ShowSession), nameof(IsStopped))]
    private string? _currentEntityName;
    [ObservableProperty] private string? _currentEntityType;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStopped), nameof(IsActive))]
    private bool _isConnecting;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLive))]
    private bool _isPaused;
    [ObservableProperty] private int _pausedMessageCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountSummary))]
    private int _messageCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBody), nameof(IsSelectedBodyJson), nameof(IsSelectedBodyXml))]
    private LiveStreamMessage? _selectedMessage;
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private string _filterText = "";

    [ObservableProperty] private object? _selectedStreamEntity;
    [ObservableProperty] private bool _hasEntities;

    public bool ShowEntitySelector => CurrentEntityName == null;
    public bool ShowSession => CurrentEntityName != null;
    public bool IsLive => IsStreaming && !IsPaused;
    public bool IsStopped => CurrentEntityName != null && !IsStreaming && !IsConnecting;
    /// <summary>True while the service is polling or retrying the current source.</summary>
    public bool IsActive => IsStreaming || IsConnecting;
    public int MaxCapturedMessages => MaxMessages;

    public string CountSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FilterText))
                return $"{FilteredMessages.Count} of {MessageCount} match";

            return MessageCount >= MaxMessages
                ? $"Newest {MaxMessages} kept"
                : $"{MessageCount} captured";
        }
    }

    public string? SelectedBody => MessageBodyFormat.Format(SelectedMessage?.Body);
    public bool IsSelectedBodyJson => MessageBodyFormat.IsJson(SelectedMessage?.Body);
    public bool IsSelectedBodyXml => MessageBodyFormat.IsXml(SelectedMessage?.Body);

    public ObservableCollection<LiveStreamMessage> Messages { get; } = [];
    public ObservableCollection<LiveStreamMessage> FilteredMessages { get; } = [];

    // Available entities for streaming
    public ObservableCollection<QueueInfo> AvailableQueues { get; } = [];
    public ObservableCollection<TopicInfo> AvailableTopics { get; } = [];

    public LiveStreamViewModel(
        ILiveStreamService liveStreamService,
        Func<IServiceBusOperations?> getOperations,
        ICorrelationMessageCatalog? correlationCatalog = null,
        Func<CorrelationSourceContext?>? getCorrelationContext = null)
    {
        _liveStreamService = liveStreamService;
        _getOperations = getOperations;
        _correlationCatalog = correlationCatalog;
        _getCorrelationContext = getCorrelationContext;
        _liveStreamService.StreamingStatusChanged += OnStreamingStatusChanged;
        _liveStreamService.StreamError += OnStreamError;

        // Subscribe to message stream
        _messageSubscription = _liveStreamService.Messages
            .ObserveOn(System.Reactive.Concurrency.Scheduler.Default)
            .Subscribe(OnMessageReceived);
    }

    private void OnStreamingStatusChanged(object? sender, bool isStreaming)
    {
        RunOnUiThread(() =>
        {
            IsStreaming = isStreaming;
            IsConnecting = false;
            if (isStreaming)
            {
                ErrorMessage = null;
            }
        });
    }

    private void OnStreamError(object? sender, Exception ex)
    {
        RunOnUiThread(() =>
        {
            // The service keeps retrying after poll errors, so connecting state is left to StreamingStatusChanged.
            ErrorMessage = ex.Message;
        });
    }

    private void OnMessageReceived(LiveStreamMessage message)
    {
        lock (_messageLock)
        {
            if (message.SequenceNumber <= _lastSequenceNumber)
                return;

            _lastSequenceNumber = message.SequenceNumber;
            _pendingMessages.Add(message);
            if (_pendingMessages.Count > MaxMessages)
            {
                _pendingMessages.RemoveRange(0, _pendingMessages.Count - MaxMessages);
            }
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

        if (SelectedMessage != null && !FilteredMessages.Contains(SelectedMessage))
        {
            SelectedMessage = null;
        }

        OnPropertyChanged(nameof(CountSummary));
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

            BeginSession(queueName, "Queue", () => StartQueueStreamAsync(queueName));

            await _liveStreamService.StartQueueStreamAsync(operations, queueName);
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

            BeginSession($"{args.topicName}/{args.subscriptionName}", "Subscription",
                () => StartSubscriptionStreamAsync(args));

            await _liveStreamService.StartSubscriptionStreamAsync(operations, args.topicName, args.subscriptionName);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsConnecting = false;
        }
    }

    private void BeginSession(string entityName, string entityType, Func<Task> restart)
    {
        // A different source starts a fresh capture; resuming the same one keeps what was captured.
        if (entityName != CurrentEntityName || entityType != CurrentEntityType)
        {
            ClearMessages();
            lock (_messageLock)
            {
                _lastSequenceNumber = 0;
            }
        }

        ErrorMessage = null;
        IsPaused = false;
        IsConnecting = true;
        CurrentEntityName = entityName;
        CurrentEntityType = entityType;
        _restartStream = restart;
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

    /// <summary>
    /// Stops polling but keeps the captured messages for review.
    /// </summary>
    [RelayCommand]
    private async Task StopStreamAsync()
    {
        await _liveStreamService.StopStreamAsync();
        IsConnecting = false;
        if (IsPaused)
        {
            TogglePause();
        }
    }

    [RelayCommand]
    private async Task ResumeStreamAsync()
    {
        if (_restartStream != null)
        {
            await _restartStream();
        }
    }

    /// <summary>
    /// Stops the stream, discards the capture and returns to source selection.
    /// </summary>
    [RelayCommand]
    private async Task ChangeSourceAsync()
    {
        await _liveStreamService.StopStreamAsync();
        ClearMessages();
        lock (_messageLock)
        {
            _lastSequenceNumber = 0;
        }

        _restartStream = null;
        IsConnecting = false;
        IsPaused = false;
        ErrorMessage = null;
        CurrentEntityName = null;
        CurrentEntityType = null;
    }

    /// <summary>
    /// Holds incoming messages so the list stops moving; resuming shows them all.
    /// </summary>
    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        if (!IsPaused)
        {
            ScheduleFlush();
        }
    }

    [RelayCommand]
    private Task CopySelectedBodyAsync() => SetClipboardTextAsync(SelectedBody);

    [RelayCommand]
    private Task CopySelectedMessageIdAsync() => SetClipboardTextAsync(SelectedMessage?.MessageId);

    private static async Task SetClipboardTextAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
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
        SelectedMessage = null;
        MessageCount = 0;
        PausedMessageCount = 0;
        OnPropertyChanged(nameof(CountSummary));
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

        _ = LoadMissingSubscriptionsAsync();
    }

    /// <summary>
    /// Loads subscriptions for topics that haven't been expanded in the sidebar yet,
    /// so they can be selected for streaming directly from this panel.
    /// </summary>
    private async Task LoadMissingSubscriptionsAsync()
    {
        var operations = _getOperations();
        if (operations == null) return;

        foreach (var topic in AvailableTopics.Where(t => !t.SubscriptionsLoaded && !t.IsLoadingSubscriptions).ToList())
        {
            RunOnUiThread(() => topic.IsLoadingSubscriptions = true);
            try
            {
                var subs = (await operations.GetSubscriptionsAsync(topic.Name)).ToList();
                RunOnUiThread(() =>
                {
                    topic.Subscriptions.Clear();
                    foreach (var sub in subs)
                    {
                        topic.Subscriptions.Add(sub);
                    }
                    topic.SubscriptionCount = subs.Count;
                    topic.SubscriptionsLoaded = true;
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to load subscriptions for topic {TopicName}", topic.Name);
            }
            finally
            {
                RunOnUiThread(() => topic.IsLoadingSubscriptions = false);
            }
        }
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
        if (Application.Current is null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            FlushPendingMessages();
            return;
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(FlushPendingMessages);
    }

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current is null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(action);
    }

    private void FlushPendingMessages()
    {
        List<LiveStreamMessage> newMessages;
        List<LiveStreamMessage> removedMessages = [];

        lock (_messageLock)
        {
            if (IsPaused)
            {
                PausedMessageCount = _pendingMessages.Count;
                Interlocked.Exchange(ref _isFlushScheduled, 0);
                return;
            }

            if (_pendingMessages.Count == 0)
            {
                Interlocked.Exchange(ref _isFlushScheduled, 0);
                if (_pendingMessages.Count > 0)
                {
                    ScheduleFlush();
                }
                return;
            }

            newMessages = new List<LiveStreamMessage>(_pendingMessages);
            _pendingMessages.Clear();
            _messageBuffer.AddRange(newMessages);

            var overflow = _messageBuffer.Count - MaxMessages;
            if (overflow > 0)
            {
                removedMessages = _messageBuffer.Take(overflow).ToList();
                _messageBuffer.RemoveRange(0, overflow);
            }
        }

        var previousNewestMessage = FilteredMessages.FirstOrDefault();

        // Messages and FilteredMessages are newest-first via Insert(0, ...), so tail removal drops oldest buffered items.
        for (var i = 0; i < removedMessages.Count; i++)
        {
            if (Messages.Count > 0)
            {
                Messages.RemoveAt(Messages.Count - 1);
            }

            if (FilteredMessages.Count > 0 && MatchesFilter(removedMessages[i]))
            {
                FilteredMessages.Remove(removedMessages[i]);
            }
        }

        for (var i = 0; i < newMessages.Count; i++)
        {
            var message = newMessages[i];
            Messages.Insert(0, message);
            if (MatchesFilter(message))
            {
                FilteredMessages.Insert(0, message);
            }

            var correlationContext = _getCorrelationContext?.Invoke();
            if (_correlationCatalog != null && correlationContext != null)
            {
                _correlationCatalog.Add(CorrelationMessageFactory.FromLiveStream(message, correlationContext));
            }
        }

        if (AutoScroll && FilteredMessages.Count > 0 && ReferenceEquals(SelectedMessage, previousNewestMessage))
        {
            SelectedMessage = FilteredMessages[0];
        }

        MessageCount = _messageBuffer.Count;
        PausedMessageCount = 0;
        ErrorMessage = null;
        OnPropertyChanged(nameof(CountSummary));
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
