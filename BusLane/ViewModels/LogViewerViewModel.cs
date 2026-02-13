using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using BusLane.Models.Logging;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

/// <summary>
/// ViewModel for the Log Viewer panel.
/// </summary>
public partial class LogViewerViewModel : ViewModelBase, IDisposable
{
    private readonly ILogSink _logSink;
    private readonly List<LogEntry> _allLogs = new();
    private CancellationTokenSource? _searchFilterCts;
    private const int SearchFilterDebounceMilliseconds = 150;
    private const int MaxLogEntries = 1000;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isAutoScrollEnabled = true;

    [ObservableProperty]
    private bool _isDebugModeEnabled;

    [ObservableProperty]
    private LogLevel? _selectedLevelFilter = LogLevel.Info;

    [ObservableProperty]
    private LogSource? _selectedSourceFilter = LogSource.Application;

    [ObservableProperty]
    private string _searchText = string.Empty;

    private ObservableCollection<LogEntry> _filteredLogs = new();
    public ObservableCollection<LogEntry> FilteredLogs => _filteredLogs;

    public int TotalLogCount => _allLogs.Count;
    public int ShowingLogCount => _filteredLogs.Count;

    /// <summary>
    /// Options for level filter (includes "All" option).
    /// </summary>
    public List<LogLevel?> LevelFilterOptions { get; } = new()
    {
        null,
        LogLevel.Info,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Debug
    };

    /// <summary>
    /// Options for source filter (includes "All" option).
    /// </summary>
    public List<LogSource?> SourceFilterOptions { get; } = new()
    {
        null,
        LogSource.Application,
        LogSource.ServiceBus
    };

    public ICommand ClearLogsCommand { get; }
    public ICommand CopyAllCommand { get; }
    public ICommand ToggleAutoScrollCommand { get; }
    public ICommand CopyEntryCommand { get; }
    public ICommand ClearSearchCommand { get; }

    /// <summary>
    /// Opens the log viewer panel.
    /// </summary>
    public void Open() => IsOpen = true;

    /// <summary>
    /// Closes the log viewer panel.
    /// </summary>
    [RelayCommand]
    public void Close() => IsOpen = false;

    /// <summary>
    /// Toggles the log viewer panel visibility.
    /// </summary>
    public void Toggle() => IsOpen = !IsOpen;

    public LogViewerViewModel(ILogSink logSink)
    {
        _logSink = logSink;
        ClearLogsCommand = new RelayCommand(ClearLogs);
        CopyAllCommand = new AsyncRelayCommand(CopyAllAsync);
        ToggleAutoScrollCommand = new RelayCommand(() => IsAutoScrollEnabled = !IsAutoScrollEnabled);
        CopyEntryCommand = new RelayCommand<LogEntry>(CopyEntry);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        // Initial load
        RefreshLogs();

        // Subscribe to new logs
        _logSink.OnLogAdded += OnLogAdded;
    }

    private void OnLogAdded(LogEntry entry)
    {
        // Marshal all operations to UI thread for thread safety
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _allLogs.Insert(0, entry);
            TrimLogCollection(_allLogs);

            if (MatchesFilters(entry))
            {
                _filteredLogs.Insert(0, entry);
                TrimLogCollection(_filteredLogs);
                OnPropertyChanged(nameof(ShowingLogCount));
            }

            OnPropertyChanged(nameof(TotalLogCount));
        });
    }

    private void RefreshLogs()
    {
        _allLogs.Clear();
        _allLogs.AddRange(_logSink.GetLogs());

        ApplyFilters();
        OnPropertyChanged(nameof(TotalLogCount));
    }

    private void ApplyFilters()
    {
        var query = _allLogs.AsEnumerable();

        // Apply level filter
        if (SelectedLevelFilter.HasValue)
        {
            query = query.Where(e => e.Level == SelectedLevelFilter.Value);
        }

        // Apply source filter
        if (SelectedSourceFilter.HasValue)
        {
            query = query.Where(e => e.Source == SelectedSourceFilter.Value);
        }

        // Apply search text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(e =>
                e.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (e.Details?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Apply debug mode filter
        if (!IsDebugModeEnabled)
        {
            query = query.Where(e => e.Level != LogLevel.Debug);
        }

        var filtered = query.ToList();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _filteredLogs.Clear();
            foreach (var entry in filtered)
            {
                _filteredLogs.Add(entry);
            }
            OnPropertyChanged(nameof(ShowingLogCount));
        });
    }

    private bool MatchesFilters(LogEntry entry)
    {
        // Apply level filter
        if (SelectedLevelFilter.HasValue && entry.Level != SelectedLevelFilter.Value)
            return false;

        // Apply source filter
        if (SelectedSourceFilter.HasValue && entry.Source != SelectedSourceFilter.Value)
            return false;

        // Apply search text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if (!entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !(entry.Details?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                return false;
        }

        // Apply debug mode filter
        if (!IsDebugModeEnabled && entry.Level == LogLevel.Debug)
            return false;

        return true;
    }

    partial void OnSelectedLevelFilterChanged(LogLevel? value)
    {
        ApplyFilters();
    }

    partial void OnSelectedSourceFilterChanged(LogSource? value)
    {
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = value;
        ScheduleSearchFilter();
    }

    partial void OnIsDebugModeEnabledChanged(bool value)
    {
        ApplyFilters();
    }

    private static void TrimLogCollection<T>(IList<T> collection)
    {
        while (collection.Count > MaxLogEntries)
        {
            collection.RemoveAt(collection.Count - 1);
        }
    }

    private void ScheduleSearchFilter()
    {
        _searchFilterCts?.Cancel();
        _searchFilterCts?.Dispose();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ApplyFilters();
            return;
        }

        var cts = new CancellationTokenSource();
        _searchFilterCts = cts;
        _ = ApplyFiltersDebouncedAsync(cts.Token);
    }

    private async Task ApplyFiltersDebouncedAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchFilterDebounceMilliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ct.IsCancellationRequested)
        {
            ApplyFilters();
        }
    }

    private void ClearLogs()
    {
        _logSink.Clear();
        _allLogs.Clear();
        _filteredLogs.Clear();
        OnPropertyChanged(nameof(TotalLogCount));
        OnPropertyChanged(nameof(ShowingLogCount));
    }

    private async Task CopyAllAsync()
    {
        var text = string.Join(Environment.NewLine, _filteredLogs.Select(e => FormatLogEntry(e)));
        await CopyToClipboard(text);
    }

    private void CopyEntry(LogEntry? entry)
    {
        if (entry == null) return;

        var text = FormatLogEntry(entry);
        FireAndForget(CopyToClipboard(text));
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");
        var source = entry.Source == LogSource.Application ? "APP" : "SB";
        var level = entry.Level.ToString().ToUpperInvariant();
        return $"[{timestamp}] [{source}] [{level}] {entry.Message}";
    }

    private async Task CopyToClipboard(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't interrupt user flow - clipboard failures are non-critical
            System.Diagnostics.Debug.WriteLine($"Clipboard operation failed: {ex.Message}");
        }
    }

    private static async void FireAndForget(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            // Log but don't interrupt user flow - fire-and-forget failures are non-critical
            System.Diagnostics.Debug.WriteLine($"Fire-and-forget operation failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _searchFilterCts?.Cancel();
        _searchFilterCts?.Dispose();
        _searchFilterCts = null;
        _logSink.OnLogAdded -= OnLogAdded;
    }
}
