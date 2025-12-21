using System.Collections.ObjectModel;
using System.Reactive.Linq;
using BusLane.Models;
using BusLane.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

public partial class LiveStreamViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ILiveStreamService _liveStreamService;
    private IDisposable? _messageSubscription;
    private const int MaxMessages = 500;

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

    public ObservableCollection<LiveStreamMessage> Messages { get; } = [];
    public ObservableCollection<LiveStreamMessage> FilteredMessages { get; } = [];

    public LiveStreamViewModel(ILiveStreamService liveStreamService)
    {
        _liveStreamService = liveStreamService;
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
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Messages.Insert(0, message);
            MessageCount = Messages.Count;
            
            // Limit messages in memory
            while (Messages.Count > MaxMessages)
            {
                Messages.RemoveAt(Messages.Count - 1);
            }
            
            // Apply filter
            if (MatchesFilter(message))
            {
                FilteredMessages.Insert(0, message);
                while (FilteredMessages.Count > MaxMessages)
                {
                    FilteredMessages.RemoveAt(FilteredMessages.Count - 1);
                }
            }
        });
    }

    partial void OnFilterTextChanged(string _)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredMessages.Clear();
        foreach (var msg in Messages.Where(MatchesFilter))
        {
            FilteredMessages.Add(msg);
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
    private async Task StartQueueStreamAsync((string endpoint, string queueName) args)
    {
        try
        {
            ErrorMessage = null;
            IsConnecting = true;
            CurrentEntityName = args.queueName;
            CurrentEntityType = "Queue";
            
            await _liveStreamService.StartQueueStreamAsync(args.endpoint, args.queueName, IsPeekMode);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task StartSubscriptionStreamAsync((string endpoint, string topicName, string subscriptionName) args)
    {
        try
        {
            ErrorMessage = null;
            IsConnecting = true;
            CurrentEntityName = $"{args.topicName}/{args.subscriptionName}";
            CurrentEntityType = "Subscription";
            
            await _liveStreamService.StartSubscriptionStreamAsync(args.endpoint, args.topicName, args.subscriptionName, IsPeekMode);
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
    public Task StartQueueAsync(string endpoint, string queueName)
    {
        return StartQueueStreamAsync((endpoint, queueName));
    }
    
    /// <summary>
    /// Start streaming from a subscription - public helper method
    /// </summary>
    public Task StartSubscriptionAsync(string endpoint, string topicName, string subscriptionName)
    {
        return StartSubscriptionStreamAsync((endpoint, topicName, subscriptionName));
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
        Messages.Clear();
        FilteredMessages.Clear();
        MessageCount = 0;
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = "";
    }

    public async ValueTask DisposeAsync()
    {
        _messageSubscription?.Dispose();
        _liveStreamService.StreamingStatusChanged -= OnStreamingStatusChanged;
        _liveStreamService.StreamError -= OnStreamError;
        await _liveStreamService.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}

