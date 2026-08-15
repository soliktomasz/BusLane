namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed record ScheduledMessageCalendarDay(
    DateOnly? Date,
    bool IsInMonth,
    IReadOnlyList<ScheduledMessageResolvedEntry> Entries)
{
    public string DisplayDay => Date?.Day.ToString() ?? "";
}

public partial class ScheduledMessagesViewModel : ViewModelBase
{
    private readonly IScheduledMessageManagementService _service;
    private readonly Func<ScheduledMessageResolvedEntry, ScheduledMessagePayload, Task> _clone;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, string> _payloadSearchText = new(StringComparer.Ordinal);

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
    [ObservableProperty] private TimeSpan? _rescheduleClockTime;

    public ObservableCollection<ScheduledMessageResolvedEntry> Entries { get; } = [];
    public IReadOnlyList<string> ConnectionOptions =>
        new[] { "All" }.Concat(Entries.Select(e => e.Entry.ConnectionName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)).ToList();
    public IReadOnlyList<string> EntityOptions =>
        new[] { "All" }.Concat(Entries.Select(e => e.Entry.EntityName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)).ToList();
    public IReadOnlyList<string> EnvironmentOptions { get; } =
        ["All", "None", "Development", "Test", "Production"];
    public IReadOnlyList<string> StatusOptions { get; } =
        ["All", "Upcoming", "Due", "Cancelled", "Rescheduled", "Action failed", "Limited", "Resolved"];
    public IReadOnlyList<string> TimeRangeOptions { get; } =
        ["All", "Today", "7 days", "30 days"];

    public IReadOnlyList<ScheduledMessageResolvedEntry> FilteredEntries =>
        Entries.Where(MatchesFilters).ToList();

    public IReadOnlyList<ScheduledMessageCalendarDay> CalendarDays
    {
        get
        {
            var first = new DateOnly(SelectedMonth.Year, SelectedMonth.Month, 1);
            var leading = ((int)first.DayOfWeek + 6) % 7;
            var start = first.AddDays(-leading);
            var byDay = FilteredEntries.GroupBy(e =>
                    DateOnly.FromDateTime(e.Entry.ScheduledEnqueueTime.LocalDateTime))
                .ToDictionary(group => group.Key, group => (IReadOnlyList<ScheduledMessageResolvedEntry>)group.ToList());
            return Enumerable.Range(0, 42)
                .Select(offset =>
                {
                    var date = start.AddDays(offset);
                    return new ScheduledMessageCalendarDay(
                        date,
                        date.Month == SelectedMonth.Month,
                        byDay.GetValueOrDefault(date, []));
                })
                .ToList();
        }
    }

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
    private async Task RefreshAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorText = null;
        try
        {
            Entries.Clear();
            _payloadSearchText.Clear();
            foreach (var entry in await _service.RefreshAsync(ct))
            {
                Entries.Add(entry);
                var payload = await _service.LoadPayloadAsync(entry.Entry, ct);
                if (payload is not null)
                {
                    _payloadSearchText[entry.Entry.RecordId] = string.Join('\n',
                        new[] { payload.Body }
                            .Concat(payload.Properties.SelectMany(p =>
                                new[] { p.Key, p.Value.Value })));
                }
            }
            if (!ConnectionOptions.Contains(SelectedConnection, StringComparer.OrdinalIgnoreCase))
            {
                SelectedConnection = "All";
            }
            if (!EntityOptions.Contains(SelectedEntity, StringComparer.OrdinalIgnoreCase))
            {
                SelectedEntity = "All";
            }
            NotifyFilterOptionsChanged();
            NotifyProjectionChanged();
        }
        catch (OperationCanceledException)
        {
            throw;
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
    [RelayCommand] private void SelectEntry(ScheduledMessageResolvedEntry? item) => SelectedEntry = item;

    [RelayCommand]
    private async Task CloneAsync(ScheduledMessageResolvedEntry? item, CancellationToken ct)
    {
        if (item is null) return;
        var payload = await _service.LoadPayloadAsync(item.Entry, ct);
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
        IsProductionAcknowledged = false;
        ConfirmationAction = "Cancel";
        ConfirmationText = BuildConfirmationText(item.Entry);
        RescheduleTime = item.Entry.ScheduledEnqueueTime;
        RescheduleClockTime = item.Entry.ScheduledEnqueueTime.TimeOfDay;
        ShowConfirmation = true;
    }

    [RelayCommand]
    private void BeginReschedule(ScheduledMessageResolvedEntry? item)
    {
        if (item is null) return;
        SelectedEntry = item;
        IsProductionAcknowledged = false;
        ConfirmationAction = "Reschedule";
        ConfirmationText = BuildConfirmationText(item.Entry);
        ShowConfirmation = true;
    }

    [RelayCommand]
    private async Task ConfirmActionAsync(CancellationToken ct)
    {
        if (SelectedEntry is null) return;
        if (ConfirmationAction == "Reschedule" && RescheduleTime is not null &&
            RescheduleClockTime is not null)
        {
            RescheduleTime = new DateTimeOffset(
                RescheduleTime.Value.Date.Add(RescheduleClockTime.Value),
                RescheduleTime.Value.Offset);
        }
        if (ConfirmationAction == "Reschedule" &&
            (RescheduleTime is null || RescheduleClockTime is null ||
             RescheduleTime <= _timeProvider.GetUtcNow()))
        {
            StatusText = "A future scheduled time is required.";
            return;
        }
        var request = new ScheduledMessageActionRequest(
            SelectedEntry.Entry, true, IsProductionAcknowledged, RescheduleTime);
        var result = ConfirmationAction == "Reschedule"
            ? await _service.RescheduleAsync(request, ct)
            : await _service.CancelAsync(request, ct);
        StatusText = result.Message;
        ShowConfirmation = false;
        IsProductionAcknowledged = false;
        await RefreshAsync(ct);
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
    private async Task ResolveAsync(ScheduledMessageResolvedEntry? item, CancellationToken ct)
    {
        if (item is null) return;
        await _service.ResolveLocallyAsync(item.Entry, ct);
        StatusText = "Record resolved locally.";
        await RefreshAsync(ct);
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
        var now = _timeProvider.GetUtcNow();
        if (SelectedTimeRange == "Today" && entry.ScheduledEnqueueTime.Date != now.Date) return false;
        if (SelectedTimeRange == "7 days" &&
            (entry.ScheduledEnqueueTime < now || entry.ScheduledEnqueueTime > now.AddDays(7))) return false;
        if (SelectedTimeRange == "30 days" &&
            (entry.ScheduledEnqueueTime < now || entry.ScheduledEnqueueTime > now.AddDays(30))) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var search = SearchText.Trim();
        return Contains(entry.ConnectionName, search) || Contains(entry.EntityName, search) ||
               Contains(entry.MessageId, search) || Contains(entry.CorrelationId, search) ||
               Contains(entry.Subject, search) ||
               entry.SearchableProperties.Keys.Any(key => Contains(key, search)) ||
               (_payloadSearchText.TryGetValue(entry.RecordId, out var payloadText) &&
                Contains(payloadText, search));
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

    private void NotifyFilterOptionsChanged()
    {
        OnPropertyChanged(nameof(ConnectionOptions));
        OnPropertyChanged(nameof(EntityOptions));
    }

    partial void OnSearchTextChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedConnectionChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedEntityChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedEnvironmentChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedStatusChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedTimeRangeChanged(string value) => NotifyProjectionChanged();
    partial void OnSelectedMonthChanged(DateTimeOffset value) => NotifyProjectionChanged();
}
