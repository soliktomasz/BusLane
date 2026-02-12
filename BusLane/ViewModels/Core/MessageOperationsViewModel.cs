using System.Collections.ObjectModel;
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
        Action<string> setStatus)
    {
        _getOperations = getOperations;
        _preferencesService = preferencesService;
        _logSink = logSink;
        _getEntityName = getEntityName;
        _getSubscriptionName = getSubscriptionName;
        _getRequiresSession = getRequiresSession;
        _getShowDeadLetter = getShowDeadLetter;
        _setStatus = setStatus;

        SelectedMessages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSelectedMessages));
            OnPropertyChanged(nameof(SelectedMessagesCount));
            OnPropertyChanged(nameof(CanResubmitDeadLetters));
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
        ApplyMessageFilter();
    }

    private void ApplyMessageFilter()
    {
        FilteredMessages.Clear();

        var filtered = string.IsNullOrWhiteSpace(MessageSearchText)
            ? Messages
            : Messages.Where(m =>
                (m.MessageId?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Body?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.CorrelationId?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Subject?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.DeadLetterReason?.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                m.SequenceNumber.ToString().Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var msg in filtered)
            FilteredMessages.Add(msg);
        
        // Notify UI that FilteredMessages has changed
        OnPropertyChanged(nameof(FilteredMessages));
    }

    [RelayCommand]
    public void ClearMessageSearch() => MessageSearchText = "";

    [RelayCommand]
    public async Task LoadMessagesAsync()
    {
        var entityName = _getEntityName();
        if (entityName == null) return;

        await LoadFirstPageAsync(entityName, _getSubscriptionName(), _getShowDeadLetter(), _getRequiresSession());
    }

    public async Task LoadFirstPageAsync(
        string entityName,
        string? subscription,
        bool deadLetter,
        bool requiresSession)
    {
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

            // Clear cache and reset pagination
            _pageCache.Clear();
            Pagination.Reset();
            Messages.Clear();
            FilteredMessages.Clear();
            SelectedMessages.Clear();
            SelectedMessage = null;

            // Load first page
            var page1Messages = await LoadPageAsync(1, null);

            if (page1Messages.Any())
            {
                _pageCache.StorePage(1, page1Messages);
                DisplayPage(1);

                // Check if there might be more messages
                var hasMore = page1Messages.Count >= _preferencesService.MessagesPerPage &&
                             _pageCache.GetTotalCachedMessages() < _preferencesService.MaxTotalMessages;
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
        var operations = _getOperations();
        if (operations == null) return;

        IsLoadingMessages = true;
        _setStatus("Loading next page...");

        try
        {
            var nextPage = Pagination.CurrentPage + 1;
            var lastSeqNum = _pageCache.GetLastSequenceNumber(Pagination.CurrentPage);

            // Check max messages limit
            if (_pageCache.GetTotalCachedMessages() >= _preferencesService.MaxTotalMessages)
            {
                _setStatus($"Maximum message limit ({_preferencesService.MaxTotalMessages}) reached");
                Pagination.CanGoNext = false;
                return;
            }

            var pageMessages = await LoadPageAsync(nextPage, lastSeqNum);

            if (pageMessages.Any())
            {
                _pageCache.StorePage(nextPage, pageMessages);
                Pagination.GoToNextPage();
                DisplayPage(nextPage);

                var hasMore = pageMessages.Count >= _preferencesService.MessagesPerPage &&
                             _pageCache.GetTotalCachedMessages() < _preferencesService.MaxTotalMessages;
                Pagination.UpdatePageInfo(nextPage, _preferencesService.MessagesPerPage, pageMessages.Count, hasMore);

                _setStatus($"Loaded page {nextPage}");
            }
            else
            {
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

    private async Task<IReadOnlyList<MessageInfo>> LoadPageAsync(int pageNumber, long? fromSequenceNumber)
    {
        var operations = _getOperations();
        if (operations == null) return new List<MessageInfo>().AsReadOnly();

        var count = _preferencesService.MessagesPerPage;
        var messages = await operations.PeekMessagesAsync(
            _currentEntityName!,
            _currentSubscription,
            count,
            fromSequenceNumber,
            _currentDeadLetter,
            _currentRequiresSession);

        var sorted = SortDescending
            ? messages.OrderByDescending(m => m.EnqueuedTime)
            : messages.OrderBy(m => m.EnqueuedTime);

        return sorted.ToList().AsReadOnly();
    }

    private void DisplayPage(int pageNumber)
    {
        Dispatcher.UIThread.Post(() =>
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
        });
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

