namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform.Storage;
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class CorrelationExplorerViewModel : ViewModelBase
{
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
    private CorrelationExplorerFilter _activeFilter = CorrelationExplorerFilter.Empty;

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

    public ObservableCollection<CorrelationGroup> Groups { get; } = [];
    public ObservableCollection<CorrelationMessage> Timeline { get; } = [];
    public ObservableCollection<ReplayAuditEntry> ReplayHistory { get; } = [];

    public CorrelationExplorerViewModel(
        ICorrelationMessageCatalog catalog,
        IReplayAuditStore auditStore,
        IMessageReplayService replayService,
        Func<IServiceBusOperations?> getOperations,
        Func<IReadOnlyList<ReplayDestination>> getDestinations,
        IFileDialogService? fileDialogService = null,
        ICorrelationMessageFilter? messageFilter = null)
    {
        _catalog = catalog;
        _auditStore = auditStore;
        _replayService = replayService;
        _getOperations = getOperations;
        _getDestinations = getDestinations;
        _fileDialogService = fileDialogService;
        _messageFilter = messageFilter ?? new CorrelationMessageFilter();
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

    private void RefreshGroups()
    {
        var selectedKey = SelectedGroup?.Key;
        var selectedMessageIdentity = SelectedMessage == null
            ? (CorrelationMessageIdentity?)null
            : CorrelationMessageIdentity.From(SelectedMessage);
        var groups = _catalog.GetGroups();
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

    private async Task ReloadHistoryAsync(CancellationToken ct)
    {
        ReplayHistory.Clear();
        foreach (var entry in (await _auditStore.LoadAsync(ct)).OrderByDescending(static item => item.Timestamp))
        {
            ReplayHistory.Add(entry);
        }
    }
}
