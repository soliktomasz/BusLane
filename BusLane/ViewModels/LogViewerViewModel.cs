using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
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

    [ObservableProperty]
    private bool _isAutoScrollEnabled = true;

    [ObservableProperty]
    private bool _isDebugModeEnabled;

    [ObservableProperty]
    private LogLevel? _selectedLevelFilter;

    [ObservableProperty]
    private LogSource? _selectedSourceFilter;

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
        // Update all logs list
        _allLogs.Add(entry);

        // Apply filters
        if (MatchesFilters(entry))
        {
            // Add to filtered collection on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _filteredLogs.Insert(0, entry);
                OnPropertyChanged(nameof(ShowingLogCount));
            });
        }

        OnPropertyChanged(nameof(TotalLogCount));
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
            var searchLower = SearchText.ToLowerInvariant();
            query = query.Where(e =>
                e.Message.ToLowerInvariant().Contains(searchLower) ||
                (e.Details?.ToLowerInvariant().Contains(searchLower) ?? false));
        }

        // Apply debug mode filter
        if (!IsDebugModeEnabled)
        {
            query = query.Where(e => e.Level != LogLevel.Debug);
        }

        var filtered = query.OrderByDescending(e => e.Timestamp).ToList();

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
            var searchLower = SearchText.ToLowerInvariant();
            if (!entry.Message.ToLowerInvariant().Contains(searchLower) &&
                !(entry.Details?.ToLowerInvariant().Contains(searchLower) ?? false))
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
        ApplyFilters();
    }

    partial void OnIsDebugModeEnabledChanged(bool value)
    {
        ApplyFilters();
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
        catch
        {
            // Silently fail - clipboard operations can fail in various scenarios
        }
    }

    private static async void FireAndForget(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Ignore exceptions in fire-and-forget operations
        }
    }

    public void Dispose()
    {
        _logSink.OnLogAdded -= OnLogAdded;
    }
}
