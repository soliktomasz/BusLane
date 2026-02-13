using System.Collections.ObjectModel;
using System.Threading;
using Avalonia.Threading;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.Infrastructure;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Handles all message-related operations: loading, filtering, selection, bulk operations.
/// </summary>
public partial class MessageOperationsViewModel : ViewModelBase
{
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly IPreferencesService _preferencesService;
    private readonly ILogSink _logSink;
    private readonly Func<string?> _getEntityName;
    private readonly Func<string?> _getSubscriptionName;
    private readonly Func<bool> _getRequiresSession;
    private readonly Func<bool> _getShowDeadLetter;
    private readonly Func<long> _getKnownMessageCount;
    private readonly Action<string> _setStatus;

    private string GetEntityDisplayName()
    {
        var entityName = _getEntityName() ?? "Unknown";
        var subscription = _getSubscriptionName();
        var dlq = _getShowDeadLetter() ? " (DLQ)" : "";
        return subscription != null ? $"{entityName}/{subscription}{dlq}" : $"{entityName}{dlq}";
    }

    [ObservableProperty] private bool _isLoadingMessages;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedMessageBody))]
    [NotifyPropertyChangedFor(nameof(IsMessageBodyJson))]
    [NotifyPropertyChangedFor(nameof(IsMessageBodyXml))]
    [NotifyPropertyChangedFor(nameof(FormattedApplicationProperties))]
    private MessageInfo? _selectedMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMessages))]
    [NotifyPropertyChangedFor(nameof(SelectedMessagesCount))]
    [NotifyPropertyChangedFor(nameof(CanResubmitDeadLetters))]
    private bool _isMultiSelectMode;

    [ObservableProperty] private int _selectionVersion;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortButtonText))]
    private bool _sortDescending = true;

    [ObservableProperty] private string _messageSearchText = "";

    public ObservableCollection<MessageInfo> Messages { get; } = [];
    public ObservableCollection<MessageInfo> FilteredMessages { get; } = [];
    public ObservableCollection<MessageInfo> SelectedMessages { get; } = [];

    public PaginationState Pagination { get; } = new();
    private readonly MessagePageCache _pageCache = new();
    private string? _currentEntityName;
    private string? _currentSubscription;
    private bool _currentDeadLetter;
    private bool _currentRequiresSession;
    private long _knownTotalCount;
    private long? _nextFromSequenceNumber;
    private CancellationTokenSource? _messageFilterCts;
    private const int MessageFilterDebounceMilliseconds = 150;

    public bool HasSelectedMessages => SelectedMessages.Count > 0;
    public int SelectedMessagesCount => SelectedMessages.Count;
    public bool CanResubmitDeadLetters => HasSelectedMessages && _getShowDeadLetter();
    public string SortButtonText => SortDescending ? "↓ Newest" : "↑ Oldest";

    public bool IsMessageBodyJson
    {
        get
        {
            if (SelectedMessage?.Body == null) return false;
            var trimmed = SelectedMessage.Body.Trim();
            return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                   (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
        }
    }

    public bool IsMessageBodyXml
    {
        get
        {
            if (SelectedMessage?.Body == null) return false;
            var trimmed = SelectedMessage.Body.Trim();
            return trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                   (trimmed.StartsWith("<") && trimmed.Contains("</") && trimmed.EndsWith(">"));
        }
    }

    public string? FormattedMessageBody
    {
        get
        {
            if (SelectedMessage?.Body == null) return null;
            if (!IsMessageBodyJson) return SelectedMessage.Body;

            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(SelectedMessage.Body);
                return System.Text.Json.JsonSerializer.Serialize(jsonDoc.RootElement,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return SelectedMessage.Body;
            }
        }
    }

    public string? FormattedApplicationProperties
    {
        get
        {
            if (SelectedMessage?.Properties == null || SelectedMessage.Properties.Count == 0)
                return null;

            try
            {
                return System.Text.Json.JsonSerializer.Serialize(SelectedMessage.Properties,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return string.Join("\n", SelectedMessage.Properties.Select(p => $"{p.Key}: {p.Value}"));
            }
        }
    }

    public MessageOperationsViewModel(
        Func<IServiceBusOperations?> getOperations,
        IPreferencesService preferencesService,
        ILogSink logSink,
        Func<string?> getEntityName,
        Func<string?> getSubscriptionName,
        Func<bool> getRequiresSession,
        Func<bool> getShowDeadLetter,
        Func<long> getKnownMessageCount,
        Action<string> setStatus)
    {
        _getOperations = getOperations;
        _preferencesService = preferencesService;
        _logSink = logSink;
        _getEntityName = getEntityName;
        _getSubscriptionName = getSubscriptionName;
        _getRequiresSession = getRequiresSession;
        _getShowDeadLetter = getShowDeadLetter;
        _getKnownMessageCount = getKnownMessageCount;
        _setStatus = setStatus;

        SelectedMessages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSelectedMessages));
            OnPropertyChanged(nameof(SelectedMessagesCount));
            OnPropertyChanged(nameof(CanResubmitDeadLetters));
        };

        // Subscribe to Pagination property changes to update command CanExecute
        Pagination.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaginationState.CanGoNext))
            {
                OnPropertyChanged(nameof(CanLoadNextPage));
                LoadNextPageCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(PaginationState.CanGoPrevious))
            {
                OnPropertyChanged(nameof(CanLoadPreviousPage));
                LoadPreviousPageCommand.NotifyCanExecuteChanged();
            }
        };
    }

    partial void OnIsMultiSelectModeChanged(bool value)
    {
        if (!value)
        {
            SelectedMessages.Clear();
            SelectionVersion++;
        }
    }

    partial void OnMessageSearchTextChanged(string value)
    {
        _ = value;
        DebounceApplyMessageFilter();
    }

    private void ApplyMessageFilter()
    {
        FilteredMessages.Clear();

        var searchText = MessageSearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(searchText);
        var isSequenceSearch = long.TryParse(searchText, out var sequenceNumberSearch);

        foreach (var message in Messages)
        {
            if (!hasSearch || MessageMatchesSearch(message, searchText, isSequenceSearch, sequenceNumberSearch))
            {
                FilteredMessages.Add(message);
            }
        }
    }

    private static bool MessageMatchesSearch(
        MessageInfo message,
        string searchText,
        bool isSequenceSearch,
        long sequenceNumberSearch)
    {
        if (isSequenceSearch && message.SequenceNumber == sequenceNumberSearch)
            return true;

        return (message.MessageId?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (message.Body?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (message.CorrelationId?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (message.Subject?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (message.DeadLetterReason?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void DebounceApplyMessageFilter()
    {
        _messageFilterCts?.Cancel();
        _messageFilterCts?.Dispose();

        if (string.IsNullOrWhiteSpace(MessageSearchText))
        {
            ApplyMessageFilter();
            return;
        }

        var cts = new CancellationTokenSource();
        _messageFilterCts = cts;
        _ = DebounceApplyMessageFilterAsync(cts.Token);
    }

    private async Task DebounceApplyMessageFilterAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(MessageFilterDebounceMilliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!ct.IsCancellationRequested)
            {
                ApplyMessageFilter();
            }
        }, DispatcherPriority.Background);
    }

    [RelayCommand]
    public void ClearMessageSearch() => MessageSearchText = "";

    [RelayCommand]
    public async Task LoadMessagesAsync()
    {
        if (IsLoadingMessages)
        {
            return;
        }

        var entityName = _getEntityName();
        if (entityName == null) return;

        var knownCount = _getKnownMessageCount();
        await LoadFirstPageAsync(entityName, _getSubscriptionName(), _getShowDeadLetter(), _getRequiresSession(), knownCount);
    }

    public async Task LoadFirstPageAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        bool requiresSession,
        long knownTotalCount = 0)
    {
        if (IsLoadingMessages)
        {
            return;
        }

        var operations = _getOperations();
        if (operations == null) return;

        var entityDisplay = GetEntityDisplayName();

        IsLoadingMessages = true;
        _setStatus("Loading messages...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.ServiceBus,
            LogLevel.Info,
            $"Loading messages from {entityDisplay}..."));

        try
        {
            // Store current context for pagination
            _currentEntityName = entityName;
            _currentSubscription = subscription;
            _currentDeadLetter = deadLetter;
            _currentRequiresSession = requiresSession;
            _knownTotalCount = knownTotalCount;

            // Clear cache and reset pagination
            _pageCache.Clear();
            Pagination.Reset();
            Messages.Clear();
            FilteredMessages.Clear();
            SelectedMessages.Clear();
            SelectedMessage = null;
            _nextFromSequenceNumber = null;

            // Load first page
            var page1Result = await LoadPageAsync(1, null);
            var page1Messages = page1Result.Messages;
            _nextFromSequenceNumber = page1Result.NextFromSequenceNumber;

            if (page1Messages.Any())
            {
                _pageCache.StorePage(1, page1Messages);
                DisplayPage(1);
                var hasMore = DetermineHasMoreMessages(1, page1Messages.Count);
                Pagination.UpdatePageInfo(1, _preferencesService.MessagesPerPage, page1Messages.Count, hasMore);

                _setStatus($"Loaded {page1Messages.Count} messages");
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.ServiceBus,
                    LogLevel.Info,
                    $"Loaded {page1Messages.Count} messages from {entityDisplay}"));
            }
            else
            {
                _setStatus("No messages found");
            }
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityNotFound)
        {
            var errorMsg = $"Entity '{entityName}' not found";
            _setStatus($"Error: {errorMsg}. Ensure you have 'Azure Service Bus Data Receiver' role assigned.");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                errorMsg,
                sbEx.Message));
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            var errorMsg = $"Failed to load messages: {sbEx.Reason}";
            _setStatus($"Error: {sbEx.Reason} - {sbEx.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                errorMsg,
                sbEx.Message));
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to load messages";
            _setStatus($"Error: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                errorMsg,
                ex.Message));
        }
        finally
        {
            IsLoadingMessages = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextPage))]
    private async Task LoadNextPageAsync()
    {
        // Prevent concurrent execution - check and set flag immediately
        if (IsLoadingMessages)
        {
            return;
        }
        IsLoadingMessages = true;

        var operations = _getOperations();
        if (operations == null)
        {
            IsLoadingMessages = false;
            return;
        }

        _setStatus("Loading next page...");

        try
        {
            var nextPage = Pagination.CurrentPage + 1;

            if (_pageCache.HasPage(nextPage))
            {
                var cachedPageMessages = _pageCache.GetPage(nextPage);
                DisplayPage(nextPage);

                var hasMoreFromCache = DetermineHasMoreMessages(nextPage, cachedPageMessages.Count);
                Pagination.UpdatePageInfo(nextPage, _preferencesService.MessagesPerPage, cachedPageMessages.Count, hasMoreFromCache);
                _setStatus($"Showing page {nextPage}");
                return;
            }

            var fromSequenceNumber = _nextFromSequenceNumber;

            // Check max messages limit
            if (_pageCache.GetTotalCachedMessages() >= _preferencesService.MaxTotalMessages)
            {
                _setStatus($"Maximum message limit ({_preferencesService.MaxTotalMessages}) reached");
                Pagination.CanGoNext = false;
                return;
            }

            var pageResult = await LoadPageAsync(nextPage, fromSequenceNumber);
            var pageMessages = pageResult.Messages;

            if (pageMessages.Any())
            {
                // Check if we actually got unseen messages (not already cached)
                var cachedSequenceNumbers = _pageCache.GetCachedSequenceNumbers();
                var gotNewMessages = pageMessages.Any(m => !cachedSequenceNumbers.Contains(m.SequenceNumber));

                if (!gotNewMessages)
                {
                    var fallbackMessages = await LoadNextPageFromStartFallbackAsync(cachedSequenceNumbers);
                    if (!fallbackMessages.Any())
                    {
                        Pagination.CanGoNext = false;
                        _setStatus("No more messages");
                        return;
                    }

                    _nextFromSequenceNumber = null;
                    _pageCache.StorePage(nextPage, fallbackMessages);
                    DisplayPage(nextPage);
                    var fallbackHasMore = DetermineHasMoreMessages(nextPage, fallbackMessages.Count);
                    Pagination.UpdatePageInfo(nextPage, _preferencesService.MessagesPerPage, fallbackMessages.Count, fallbackHasMore);
                    _setStatus($"Loaded page {nextPage}");
                    return;
                }

                _nextFromSequenceNumber = pageResult.NextFromSequenceNumber;
                _pageCache.StorePage(nextPage, pageMessages);
                DisplayPage(nextPage);
                var hasMore = DetermineHasMoreMessages(nextPage, pageMessages.Count);

                Pagination.UpdatePageInfo(nextPage, _preferencesService.MessagesPerPage, pageMessages.Count, hasMore);

                _setStatus($"Loaded page {nextPage}");
            }
            else
            {
                var fallbackMessages = await LoadNextPageFromStartFallbackAsync(_pageCache.GetCachedSequenceNumbers());
                if (fallbackMessages.Any())
                {
                    _nextFromSequenceNumber = null;
                    _pageCache.StorePage(nextPage, fallbackMessages);
                    DisplayPage(nextPage);
                    var fallbackHasMore = DetermineHasMoreMessages(nextPage, fallbackMessages.Count);
                    Pagination.UpdatePageInfo(nextPage, _preferencesService.MessagesPerPage, fallbackMessages.Count, fallbackHasMore);
                    _setStatus($"Loaded page {nextPage}");
                    return;
                }

                Pagination.CanGoNext = false;
                _setStatus("No more messages");
            }
        }
        catch (Exception ex)
        {
            _setStatus($"Error loading page: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                "Failed to load next page",
                ex.Message));
        }
        finally
        {
            IsLoadingMessages = false;
        }
    }

    public bool CanLoadNextPage => Pagination.CanGoNext;

    [RelayCommand(CanExecute = nameof(CanLoadPreviousPage))]
    private void LoadPreviousPage()
    {
        if (Pagination.CanGoPrevious)
        {
            Pagination.GoToPreviousPage();
            DisplayPage(Pagination.CurrentPage);

            var totalCached = _pageCache.GetTotalCachedMessages();
            Pagination.UpdatePageInfoWithTotal(
                Pagination.CurrentPage,
                _preferencesService.MessagesPerPage,
                totalCached);

            _setStatus($"Showing page {Pagination.CurrentPage}");
        }
    }

    public bool CanLoadPreviousPage => Pagination.CanGoPrevious;

    private async Task<PageLoadResult> LoadPageAsync(int pageNumber, long? fromSequenceNumber)
    {
        var operations = _getOperations();
        if (operations == null) return new PageLoadResult(new List<MessageInfo>().AsReadOnly(), fromSequenceNumber);

        var count = _preferencesService.MessagesPerPage;
        var messages = (await operations.PeekMessagesAsync(
            _currentEntityName!,
            _currentSubscription,
            count,
            fromSequenceNumber,
            _currentDeadLetter,
            _currentRequiresSession)).ToList();

        var sorted = SortDescending
            ? messages.OrderByDescending(m => m.EnqueuedTime)
            : messages.OrderBy(m => m.EnqueuedTime);

        var nextFrom = fromSequenceNumber;
        if (messages.Count > 0)
        {
            var maxSequenceNumber = messages.Max(static m => m.SequenceNumber);
            if (maxSequenceNumber < long.MaxValue)
            {
                nextFrom = maxSequenceNumber + 1;
            }
        }

        return new PageLoadResult(sorted.ToList().AsReadOnly(), nextFrom);
    }

    private sealed record PageLoadResult(IReadOnlyList<MessageInfo> Messages, long? NextFromSequenceNumber);

    private bool DetermineHasMoreMessages(int currentPage, int currentPageMessageCount)
    {
        if (_pageCache.HasPage(currentPage + 1))
        {
            return true;
        }

        var loadedCount = _pageCache.GetTotalCachedMessages();
        if (loadedCount >= _preferencesService.MaxTotalMessages)
        {
            return false;
        }

        // Be optimistic. We verify the actual end when a next-page fetch produces
        // no unseen messages.
        return currentPageMessageCount > 0;
    }

    private async Task<IReadOnlyList<MessageInfo>> LoadNextPageFromStartFallbackAsync(HashSet<long> cachedSequenceNumbers)
    {
        var operations = _getOperations();
        if (operations == null)
        {
            return new List<MessageInfo>().AsReadOnly();
        }

        var scanCount = Math.Min(
            _preferencesService.MaxTotalMessages,
            _pageCache.GetTotalCachedMessages() + _preferencesService.MessagesPerPage);

        var messages = await operations.PeekMessagesAsync(
            _currentEntityName!,
            _currentSubscription,
            scanCount,
            null,
            _currentDeadLetter,
            _currentRequiresSession);

        var ordered = SortDescending
            ? messages.OrderByDescending(m => m.EnqueuedTime)
            : messages.OrderBy(m => m.EnqueuedTime);

        var nextPage = ordered
            .Where(m => !cachedSequenceNumbers.Contains(m.SequenceNumber))
            .Take(_preferencesService.MessagesPerPage)
            .ToList();

        return nextPage.AsReadOnly();
    }

    private void DisplayPage(int pageNumber)
    {
        // Use Invoke to ensure UI updates happen synchronously
        // This prevents race conditions where pagination shows but messages don't
        if (Dispatcher.UIThread.CheckAccess())
        {
            DisplayPageCore(pageNumber);
        }
        else
        {
            Dispatcher.UIThread.Invoke(() => DisplayPageCore(pageNumber));
        }
    }

    private void DisplayPageCore(int pageNumber)
    {
        Messages.Clear();
        FilteredMessages.Clear();
        SelectedMessages.Clear();
        SelectedMessage = null;

        var pageMessages = _pageCache.GetPage(pageNumber);

        foreach (var message in pageMessages)
        {
            Messages.Add(message);
        }

        ApplyMessageFilter();
    }

    public void SelectMessage(MessageInfo message) => SelectedMessage = message;

    public void ClearSelectedMessage() => SelectedMessage = null;

    public void ToggleMultiSelectMode()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode) SelectedMessages.Clear();
    }

    public void ToggleMessageSelection(MessageInfo message)
    {
        if (SelectedMessages.Contains(message))
            SelectedMessages.Remove(message);
        else
            SelectedMessages.Add(message);
        SelectionVersion++;
    }

    public void SelectAllMessages()
    {
        SelectedMessages.Clear();
        foreach (var msg in FilteredMessages)
            SelectedMessages.Add(msg);
        SelectionVersion++;
    }

    public void DeselectAllMessages()
    {
        SelectedMessages.Clear();
        SelectionVersion++;
    }

    public void ToggleSortOrder()
    {
        SortDescending = !SortDescending;
        ApplySorting();
    }

    private void ApplySorting()
    {
        if (Messages.Count == 0) return;

        var sorted = SortDescending
            ? Messages.OrderByDescending(m => m.EnqueuedTime).ToList()
            : Messages.OrderBy(m => m.EnqueuedTime).ToList();

        Messages.Clear();
        foreach (var m in sorted)
            Messages.Add(m);

        ApplyMessageFilter();
    }

    public async Task CopyMessageBodyAsync()
    {
        if (FormattedMessageBody == null) return;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(FormattedMessageBody);
            }
        }
    }

    /// <summary>
    /// Clears all message state.
    /// </summary>
    public void Clear()
    {
        _messageFilterCts?.Cancel();
        _messageFilterCts?.Dispose();
        _messageFilterCts = null;

        Messages.Clear();
        FilteredMessages.Clear();
        SelectedMessages.Clear();
        SelectedMessage = null;
        IsMultiSelectMode = false;
        MessageSearchText = "";
        _pageCache.Clear();
        Pagination.Reset();
    }
}
