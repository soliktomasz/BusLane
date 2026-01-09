using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Handles all message-related operations: loading, filtering, selection, bulk operations.
/// </summary>
public partial class MessageOperationsViewModel : ViewModelBase
{
    private readonly IServiceBusService _serviceBus;
    private readonly IConnectionStringService _connectionStringService;
    private readonly IPreferencesService _preferencesService;
    private readonly Func<string?> _getEndpoint;
    private readonly Func<string?> _getConnectionString;
    private readonly Func<string?> _getEntityName;
    private readonly Func<string?> _getSubscriptionName;
    private readonly Func<bool> _getRequiresSession;
    private readonly Func<bool> _getShowDeadLetter;
    private readonly Action<string> _setStatus;

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
            return trimmed.StartsWith("<") && trimmed.EndsWith(">");
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
        IServiceBusService serviceBus,
        IConnectionStringService connectionStringService,
        IPreferencesService preferencesService,
        Func<string?> getEndpoint,
        Func<string?> getConnectionString,
        Func<string?> getEntityName,
        Func<string?> getSubscriptionName,
        Func<bool> getRequiresSession,
        Func<bool> getShowDeadLetter,
        Action<string> setStatus)
    {
        _serviceBus = serviceBus;
        _connectionStringService = connectionStringService;
        _preferencesService = preferencesService;
        _getEndpoint = getEndpoint;
        _getConnectionString = getConnectionString;
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
    }

    [RelayCommand]
    public void ClearMessageSearch() => MessageSearchText = "";

    [RelayCommand]
    public async Task LoadMessagesAsync()
    {
        var endpoint = _getEndpoint();
        var connectionString = _getConnectionString();
        var entityName = _getEntityName();

        if (entityName == null) return;

        var subscription = _getSubscriptionName();
        var requiresSession = _getRequiresSession();
        var showDeadLetter = _getShowDeadLetter();

        IsLoadingMessages = true;
        _setStatus("Loading messages...");
        MessageSearchText = "";
        SelectedMessages.Clear();

        try
        {
            Messages.Clear();
            IEnumerable<MessageInfo> msgs;

            if (!string.IsNullOrEmpty(connectionString))
            {
                msgs = await _connectionStringService.PeekMessagesAsync(
                    connectionString, entityName, subscription, _preferencesService.DefaultMessageCount, showDeadLetter, requiresSession);
            }
            else if (!string.IsNullOrEmpty(endpoint))
            {
                msgs = await _serviceBus.PeekMessagesAsync(
                    endpoint, entityName, subscription, _preferencesService.DefaultMessageCount, showDeadLetter, requiresSession);
            }
            else
            {
                return;
            }

            var sortedMsgs = SortDescending
                ? msgs.OrderByDescending(m => m.EnqueuedTime)
                : msgs.OrderBy(m => m.EnqueuedTime);

            foreach (var m in sortedMsgs)
                Messages.Add(m);

            ApplyMessageFilter();
            _setStatus($"{Messages.Count} message(s)");
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityNotFound)
        {
            _setStatus($"Error: Entity '{entityName}' not found. Ensure you have 'Azure Service Bus Data Receiver' role assigned.");
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            _setStatus($"Error: {sbEx.Reason} - {sbEx.Message}");
        }
        catch (Exception ex)
        {
            _setStatus($"Error: {ex.Message}");
        }
        finally
        {
            IsLoadingMessages = false;
        }
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
    }
}

