namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class CorrelationExplorerViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(100);
    private static readonly IReadOnlyList<FilePickerFileType> JsonFileTypes =
    [
        new("JSON Files")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"]
        }
    ];

    private readonly ICorrelationMessageCatalog _catalog;
    private readonly IReplayAuditStore _auditStore;
    private readonly IMessageReplayService _replayService;
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly Func<IReadOnlyList<ReplayDestination>> _getDestinations;
    private readonly IFileDialogService? _fileDialogService;
    private readonly ICorrelationMessageFilter _messageFilter;
    private readonly ICorrelationRefreshDelay _refreshDelay;
    private readonly ICorrelationMessageComparisonService _comparisonService;
    private readonly object _refreshLock = new();
    private CorrelationExplorerFilter _activeFilter = CorrelationExplorerFilter.Empty;
    private CancellationTokenSource? _refreshCts;
    private bool _disposed;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string? _filterFromText;
    [ObservableProperty] private string? _filterToText;
    [ObservableProperty] private string? _filterNamespace;
    [ObservableProperty] private string? _filterEntity;
    [ObservableProperty] private ConnectionEnvironment? _filterEnvironment;
    [ObservableProperty] private CorrelationMessageSource? _filterSource;
    [ObservableProperty] private string? _filterIdentifier;
    [ObservableProperty] private string? _filterPropertyKey;
    [ObservableProperty] private string? _filterPropertyValue;
    [ObservableProperty] private bool _showFilters;
    [ObservableProperty] private string? _filterValidationMessage;
    [ObservableProperty] private CorrelationGroup? _selectedGroup;
    [ObservableProperty] private CorrelationMessage? _selectedMessage;
    [ObservableProperty] private ReplayMessageViewModel? _replayEditor;
    [ObservableProperty] private bool _showReplayEditor;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewMessages))]
    private int _newMessageCount;
    [ObservableProperty] private CorrelationMessage? _comparisonMessageA;
    [ObservableProperty] private CorrelationMessage? _comparisonMessageB;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    private MessageComparison? _comparison;

    public ObservableCollection<CorrelationGroup> Groups { get; } = [];
    public ObservableCollection<CorrelationMessage> Timeline { get; } = [];
    public ObservableCollection<ReplayAuditEntry> ReplayHistory { get; } = [];
    public bool HasComparison => Comparison != null;
    public bool HasNewMessages => NewMessageCount > 0;
    public IReadOnlyList<ConnectionEnvironment> FilterEnvironmentOptions { get; } =
        Enum.GetValues<ConnectionEnvironment>();
    public IReadOnlyList<CorrelationMessageSource> FilterSourceOptions { get; } =
        Enum.GetValues<CorrelationMessageSource>();

    public CorrelationExplorerViewModel(
        ICorrelationMessageCatalog catalog,
        IReplayAuditStore auditStore,
        IMessageReplayService replayService,
        Func<IServiceBusOperations?> getOperations,
        Func<IReadOnlyList<ReplayDestination>> getDestinations,
        IFileDialogService? fileDialogService = null,
        ICorrelationMessageFilter? messageFilter = null,
        ICorrelationRefreshDelay? refreshDelay = null,
        ICorrelationMessageComparisonService? comparisonService = null)
    {
        _catalog = catalog;
        _auditStore = auditStore;
        _replayService = replayService;
        _getOperations = getOperations;
        _getDestinations = getDestinations;
        _fileDialogService = fileDialogService;
        _messageFilter = messageFilter ?? new CorrelationMessageFilter();
        _refreshDelay = refreshDelay ?? new CorrelationRefreshDelay();
        _comparisonService = comparisonService ?? new CorrelationMessageComparisonService();
        _catalog.Changed += OnCatalogChanged;
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            RefreshGroups();
            await ReloadHistoryAsync(ct);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        _activeFilter = _activeFilter with { Text = Normalize(value) };
        RefreshGroups();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    private void ToggleFilters()
    {
        ShowFilters = !ShowFilters;
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        if (!TryBuildFilter(out var filter, out var error))
        {
            FilterValidationMessage = error;
            return;
        }

        FilterValidationMessage = null;
        _activeFilter = filter!;
        RefreshGroups();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterFromText = null;
        FilterToText = null;
        FilterNamespace = null;
        FilterEntity = null;
        FilterEnvironment = null;
        FilterSource = null;
        FilterIdentifier = null;
        FilterPropertyKey = null;
        FilterPropertyValue = null;
        FilterText = string.Empty;
        FilterValidationMessage = null;
        _activeFilter = CorrelationExplorerFilter.Empty;
        RefreshGroups();
    }

    partial void OnSelectedGroupChanged(CorrelationGroup? value)
    {
        Timeline.Clear();
        if (value != null)
        {
            foreach (var message in value.Messages)
            {
                Timeline.Add(message);
            }
        }

        SelectedMessage = Timeline.FirstOrDefault();
        NewMessageCount = 0;
    }

    partial void OnSelectedMessageChanged(CorrelationMessage? value)
    {
        if (value != null &&
            Timeline.LastOrDefault() is { } latest &&
            CorrelationMessageIdentity.From(value) == CorrelationMessageIdentity.From(latest))
        {
            NewMessageCount = 0;
        }
    }

    [RelayCommand]
    private void AcknowledgeNewMessages()
    {
        NewMessageCount = 0;
    }

    partial void OnComparisonMessageAChanged(CorrelationMessage? value)
    {
        _ = value;
        RecomputeComparison();
    }

    partial void OnComparisonMessageBChanged(CorrelationMessage? value)
    {
        _ = value;
        RecomputeComparison();
    }

    [RelayCommand]
    private void SetComparisonA(CorrelationMessage? message)
    {
        if (message == null)
        {
            StatusMessage = "Select a message for comparison A";
            return;
        }

        ComparisonMessageA = message;
    }

    [RelayCommand]
    private void SetComparisonB(CorrelationMessage? message)
    {
        if (message == null)
        {
            StatusMessage = "Select a message for comparison B";
            return;
        }

        ComparisonMessageB = message;
    }

    [RelayCommand]
    private void CompareWithPrevious()
    {
        if (SelectedMessage == null)
        {
            StatusMessage = "Select a message to compare";
            return;
        }

        var index = Timeline.IndexOf(SelectedMessage);
        if (index <= 0)
        {
            StatusMessage = "The selected message has no previous timeline entry";
            return;
        }

        ComparisonMessageA = Timeline[index - 1];
        ComparisonMessageB = SelectedMessage;
        StatusMessage = null;
    }

    [RelayCommand]
    private void ClearComparison()
    {
        ComparisonMessageA = null;
        ComparisonMessageB = null;
        Comparison = null;
    }

    [RelayCommand]
    private void OpenReplay()
    {
        if (SelectedMessage == null)
        {
            StatusMessage = "Select a message to replay";
            return;
        }

        var destinations = _getDestinations();
        if (destinations.Count == 0)
        {
            StatusMessage = "No queue or topic destinations are available";
            return;
        }

        ReplayEditor = new ReplayMessageViewModel(
            SelectedMessage,
            destinations,
            _replayService,
            _getOperations,
            ReloadHistoryAsync);
        ShowReplayEditor = true;
    }

    [RelayCommand]
    private void CloseReplay()
    {
        ShowReplayEditor = false;
        ReplayEditor = null;
    }

    [RelayCommand]
    private async Task ExportHistoryAsync(CancellationToken ct = default)
    {
        if (_fileDialogService == null)
        {
            StatusMessage = "File dialog service not available";
            return;
        }

        var path = await _fileDialogService.SaveFileAsync(
            "Export Replay History",
            $"ReplayHistory_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            JsonFileTypes);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.Serialize(ReplayHistory.ToList(), options);
        await File.WriteAllTextAsync(path, json, ct);
        StatusMessage = $"Exported replay history to {Path.GetFileName(path)}";
    }

    private void RefreshGroups(bool isLiveUpdate = false)
    {
        var selectedKey = SelectedGroup?.Key;
        var selectedMessageIdentity = SelectedMessage == null
            ? (CorrelationMessageIdentity?)null
            : CorrelationMessageIdentity.From(SelectedMessage);
        var previousTimeline = Timeline
            .Select(CorrelationMessageIdentity.From)
            .ToHashSet();
        var previousNewMessageCount = NewMessageCount;
        var groups = _catalog.GetGroups();
        ReconcileComparisonSlots(groups.SelectMany(static group => group.Messages));
        groups = groups
            .Select(group => group with
            {
                Messages = group.Messages
                    .Where(message => _messageFilter.Matches(message, _activeFilter))
                    .ToList()
            })
            .Where(static group => group.Messages.Count > 0)
            .ToList();

        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(group);
        }

        SelectedGroup = Groups.FirstOrDefault(group => group.Key == selectedKey) ?? Groups.FirstOrDefault();
        if (selectedMessageIdentity.HasValue)
        {
            SelectedMessage = Timeline.FirstOrDefault(message =>
                CorrelationMessageIdentity.From(message) == selectedMessageIdentity.Value) ??
                Timeline.FirstOrDefault();
        }

        if (isLiveUpdate && SelectedGroup?.Key == selectedKey)
        {
            NewMessageCount = previousNewMessageCount + Timeline.Count(message =>
                !previousTimeline.Contains(CorrelationMessageIdentity.From(message)));
        }
    }

    private bool TryBuildFilter(out CorrelationExplorerFilter? filter, out string? error)
    {
        filter = null;
        error = null;
        if (!TryParseTimestamp(FilterFromText, "From", out var from, out error) ||
            !TryParseTimestamp(FilterToText, "To", out var to, out error))
        {
            return false;
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            error = "From time must be before or equal to To time";
            return false;
        }

        filter = new CorrelationExplorerFilter
        {
            Text = Normalize(FilterText),
            From = from,
            To = to,
            Namespace = Normalize(FilterNamespace),
            Entity = Normalize(FilterEntity),
            Environment = FilterEnvironment,
            Source = FilterSource,
            Identifier = Normalize(FilterIdentifier),
            PropertyKey = Normalize(FilterPropertyKey),
            PropertyValue = Normalize(FilterPropertyValue)
        };
        return true;
    }

    private static bool TryParseTimestamp(
        string? value,
        string label,
        out DateTimeOffset? timestamp,
        out string? error)
    {
        timestamp = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            error = $"{label} time must be a valid ISO 8601 timestamp";
            return false;
        }

        timestamp = parsed;
        return true;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void RecomputeComparison()
    {
        Comparison = ComparisonMessageA != null && ComparisonMessageB != null
            ? _comparisonService.Compare(ComparisonMessageA, ComparisonMessageB)
            : null;
    }

    private void ReconcileComparisonSlots(IEnumerable<CorrelationMessage> messages)
    {
        var available = messages
            .Select(CorrelationMessageIdentity.From)
            .ToHashSet();
        var cleared = false;
        if (ComparisonMessageA != null &&
            !available.Contains(CorrelationMessageIdentity.From(ComparisonMessageA)))
        {
            ComparisonMessageA = null;
            cleared = true;
        }

        if (ComparisonMessageB != null &&
            !available.Contains(CorrelationMessageIdentity.From(ComparisonMessageB)))
        {
            ComparisonMessageB = null;
            cleared = true;
        }

        if (cleared)
        {
            StatusMessage = "A compared message is no longer available";
        }
    }

    private async Task ReloadHistoryAsync(CancellationToken ct)
    {
        ReplayHistory.Clear();
        foreach (var entry in (await _auditStore.LoadAsync(ct)).OrderByDescending(static item => item.Timestamp))
        {
            ReplayHistory.Add(entry);
        }
    }

    private void OnCatalogChanged(object? sender, CorrelationCatalogChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        CancellationTokenSource? previous;
        CancellationTokenSource current;
        lock (_refreshLock)
        {
            if (_disposed)
            {
                return;
            }

            previous = _refreshCts;
            current = new CancellationTokenSource();
            _refreshCts = current;
        }

        previous?.Cancel();
        _ = RefreshAfterDelayAsync(current);
    }

    private async Task RefreshAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await _refreshDelay.DelayAsync(RefreshDebounce, cts.Token);
            RunOnUiThread(() =>
            {
                try
                {
                    RefreshGroups(isLiveUpdate: true);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Correlation refresh failed: {ex.Message}";
                }
            });
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => StatusMessage = $"Correlation refresh failed: {ex.Message}");
        }
        finally
        {
            lock (_refreshLock)
            {
                if (ReferenceEquals(_refreshCts, cts))
                {
                    _refreshCts = null;
                }
            }

            cts.Dispose();
        }
    }

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    public void Dispose()
    {
        CancellationTokenSource? refreshCts;
        lock (_refreshLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            refreshCts = _refreshCts;
            _refreshCts = null;
        }

        _catalog.Changed -= OnCatalogChanged;
        refreshCts?.Cancel();
    }
}
