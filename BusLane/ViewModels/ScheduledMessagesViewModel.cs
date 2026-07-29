namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed record ScheduledMessageCalendarDay(
    DateOnly Date,
    IReadOnlyList<ScheduledMessageResolvedEntry> Entries);

public partial class ScheduledMessagesViewModel : ViewModelBase
{
    private readonly IScheduledMessageManagementService _service;
    private readonly Func<ScheduledMessageResolvedEntry, ScheduledMessagePayload, Task> _clone;
    private readonly TimeProvider _timeProvider;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedConnection = "All";
    [ObservableProperty] private string _selectedEntity = "All";
    [ObservableProperty] private string _selectedEnvironment = "All";
    [ObservableProperty] private string _selectedStatus = "Upcoming";
    [ObservableProperty] private string _selectedTimeRange = "All";
    [ObservableProperty] private bool _isCalendarMode;
    [ObservableProperty] private DateTimeOffset _selectedMonth;
    [ObservableProperty] private ScheduledMessageResolvedEntry? _selectedEntry;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _showConfirmation;
    [ObservableProperty] private string? _confirmationAction;
    [ObservableProperty] private string? _confirmationText;
    [ObservableProperty] private bool _isProductionAcknowledged;
    [ObservableProperty] private DateTimeOffset? _rescheduleTime;

    public ObservableCollection<ScheduledMessageResolvedEntry> Entries { get; } = [];

    public IReadOnlyList<ScheduledMessageResolvedEntry> FilteredEntries =>
        Entries.Where(MatchesFilters).ToList();

    public IReadOnlyList<ScheduledMessageCalendarDay> CalendarDays =>
        FilteredEntries
            .Where(e => e.Entry.ScheduledEnqueueTime.Year == SelectedMonth.Year &&
                        e.Entry.ScheduledEnqueueTime.Month == SelectedMonth.Month)
            .GroupBy(e => DateOnly.FromDateTime(e.Entry.ScheduledEnqueueTime.LocalDateTime))
            .Select(g => new ScheduledMessageCalendarDay(g.Key, g.ToList()))
            .OrderBy(day => day.Date)
            .ToList();

    public bool IsEmpty => FilteredEntries.Count == 0;

    public ScheduledMessagesViewModel(
        IScheduledMessageManagementService service,
        Func<ScheduledMessageResolvedEntry, ScheduledMessagePayload, Task> clone,
        TimeProvider timeProvider)
    {
        _service = service;
        _clone = clone;
        _timeProvider = timeProvider;
        _selectedMonth = timeProvider.GetUtcNow();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorText = null;
        try
        {
            Entries.Clear();
            foreach (var entry in await _service.RefreshAsync())
            {
                Entries.Add(entry);
            }
            NotifyProjectionChanged();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand] private void ShowList() => IsCalendarMode = false;
    [RelayCommand] private void ShowCalendar() => IsCalendarMode = true;
    [RelayCommand] private void PreviousMonth() => SelectedMonth = SelectedMonth.AddMonths(-1);
    [RelayCommand] private void NextMonth() => SelectedMonth = SelectedMonth.AddMonths(1);

    [RelayCommand]
    private async Task CloneAsync(ScheduledMessageResolvedEntry? item)
    {
        if (item is null) return;
        var payload = await _service.LoadPayloadAsync(item.Entry);
        if (payload is null)
        {
            StatusText = "The scheduled payload is unavailable.";
            return;
        }
        await _clone(item, payload);
    }

    [RelayCommand]
    private void BeginCancel(ScheduledMessageResolvedEntry? item)
    {
        if (item is null) return;
        SelectedEntry = item;
        ConfirmationAction = "Cancel";
        ConfirmationText = BuildConfirmationText(item.Entry);
        ShowConfirmation = true;
    }

    [RelayCommand]
    private void BeginReschedule(ScheduledMessageResolvedEntry? item)
    {
        if (item is null) return;
        SelectedEntry = item;
        ConfirmationAction = "Reschedule";
        ConfirmationText = BuildConfirmationText(item.Entry);
        ShowConfirmation = true;
    }

    [RelayCommand]
    private async Task ConfirmActionAsync()
    {
        if (SelectedEntry is null) return;
        if (ConfirmationAction == "Reschedule" &&
            (RescheduleTime is null || RescheduleTime <= _timeProvider.GetUtcNow()))
        {
            StatusText = "A future scheduled time is required.";
            return;
        }
        var request = new ScheduledMessageActionRequest(
            SelectedEntry.Entry, true, IsProductionAcknowledged, RescheduleTime);
        var result = ConfirmationAction == "Reschedule"
            ? await _service.RescheduleAsync(request)
            : await _service.CancelAsync(request);
        StatusText = result.Message;
        ShowConfirmation = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelAction()
    {
        ShowConfirmation = false;
        ConfirmationAction = null;
        SelectedEntry = null;
        IsProductionAcknowledged = false;
    }

    [RelayCommand]
    private async Task ResolveAsync(ScheduledMessageResolvedEntry? item)
    {
        if (item is null) return;
        await _service.ResolveLocallyAsync(item.Entry);
        StatusText = "Record resolved locally.";
        await RefreshAsync();
    }

    private bool MatchesFilters(ScheduledMessageResolvedEntry item)
    {
        var entry = item.Entry;
        if (SelectedStatus == "Upcoming" && item.LocalState != "Upcoming (local)") return false;
        if (SelectedStatus != "All" && SelectedStatus != "Upcoming" &&
            !item.LocalState.Contains(SelectedStatus, StringComparison.OrdinalIgnoreCase)) return false;
        if (SelectedConnection != "All" && entry.ConnectionName != SelectedConnection) return false;
        if (SelectedEntity != "All" && entry.EntityName != SelectedEntity) return false;
        if (SelectedEnvironment != "All" &&
            !entry.Environment.ToString().Equals(SelectedEnvironment, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var search = SearchText.Trim();
        return Contains(entry.ConnectionName, search) || Contains(entry.EntityName, search) ||
               Contains(entry.MessageId, search) || Contains(entry.CorrelationId, search) ||
               Contains(entry.Subject, search) ||
               entry.SearchableProperties.Keys.Any(key => Contains(key, search));
    }

    private static bool Contains(string? value, string search) =>
        value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static string BuildConfirmationText(ScheduledMessageIndexEntry entry) =>
        $"{entry.ConnectionName} · {entry.Environment} · {entry.EntityName} · " +
        $"{entry.ScheduledEnqueueTime:g} · sequence {entry.SequenceNumber}";

    private void NotifyProjectionChanged()
    {
        OnPropertyChanged(nameof(FilteredEntries));
        OnPropertyChanged(nameof(CalendarDays));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSearchTextChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedConnectionChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedEntityChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedEnvironmentChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedStatusChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedTimeRangeChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedMonthChanged(DateTimeOffset value) => NotifyProjectionChanged();
}
