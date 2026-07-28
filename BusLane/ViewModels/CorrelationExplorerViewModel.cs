namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
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

    [ObservableProperty] private string _filterText = string.Empty;
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
        IFileDialogService? fileDialogService = null)
    {
        _catalog = catalog;
        _auditStore = auditStore;
        _replayService = replayService;
        _getOperations = getOperations;
        _getDestinations = getDestinations;
        _fileDialogService = fileDialogService;
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
        _ = value;
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
        var groups = _catalog.GetGroups();
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            groups = groups
                .Where(group =>
                    group.DisplayId.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    group.Messages.Any(message =>
                        message.MessageId.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                        message.EntityName.Contains(FilterText, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(group);
        }

        SelectedGroup = Groups.FirstOrDefault(group => group.Key == selectedKey) ?? Groups.FirstOrDefault();
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
