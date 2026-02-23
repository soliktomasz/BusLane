namespace BusLane.ViewModels.Core;

using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using BusLane.Services.Abstractions;
using BusLane.Services.Terminal;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Manages terminal visibility, docking state, persisted layout, and session lifecycle.
/// </summary>
public partial class TerminalHostViewModel : ViewModelBase, IDisposable, IAsyncDisposable
{
    private const int MaxOutputLines = 10000;
    private const double MinDockHeight = 140;
    private const double MaxDockHeight = 700;

    private readonly ITerminalSessionService _terminalSession;
    private readonly IPreferencesService _preferencesService;
    private readonly Action<string>? _setStatus;
    private readonly Queue<string> _outputLines = new();
    private readonly StringBuilder _currentLine = new();

    private bool _isInitializing;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DockedHeight))]
    [NotifyPropertyChangedFor(nameof(IsDockedVisible))]
    [NotifyPropertyChangedFor(nameof(IsUndockedVisible))]
    private bool _showTerminalPanel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DockedHeight))]
    [NotifyPropertyChangedFor(nameof(IsDockedVisible))]
    [NotifyPropertyChangedFor(nameof(IsUndockedVisible))]
    private bool _terminalIsDocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DockedHeight))]
    private double _terminalDockHeight;

    [ObservableProperty]
    private string? _terminalWindowBoundsJson;

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorBrush))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorTooltip))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorBrush))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorTooltip))]
    private string _sessionStatus = "Stopped";

    public bool IsDockedVisible => ShowTerminalPanel && TerminalIsDocked;
    public bool IsUndockedVisible => ShowTerminalPanel && !TerminalIsDocked;
    public double DockedHeight => IsDockedVisible ? TerminalDockHeight : 0;
    public Guid SessionId => _terminalSession.SessionId;
    public string StatusIndicatorTooltip => SessionStatus;

    public string StatusIndicatorBrush
    {
        get
        {
            if (SessionStatus.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                return "#ef4444";
            }

            if (SessionStatus.StartsWith("Starting", StringComparison.OrdinalIgnoreCase) ||
                SessionStatus.StartsWith("Restarting", StringComparison.OrdinalIgnoreCase))
            {
                return "#f59e0b";
            }

            if (SessionStatus.StartsWith("Exited", StringComparison.OrdinalIgnoreCase) ||
                SessionStatus.StartsWith("Stopped", StringComparison.OrdinalIgnoreCase))
            {
                return "#94a3b8";
            }

            return IsRunning ? "#22c55e" : "#94a3b8";
        }
    }

    public TerminalHostViewModel(
        ITerminalSessionService terminalSession,
        IPreferencesService preferencesService,
        Action<string>? setStatus = null)
    {
        _terminalSession = terminalSession;
        _preferencesService = preferencesService;
        _setStatus = setStatus;

        _isInitializing = true;
        _showTerminalPanel = preferencesService.ShowTerminalPanel;
        _terminalIsDocked = preferencesService.TerminalIsDocked;
        _terminalDockHeight = ClampDockHeight(preferencesService.TerminalDockHeight);
        _terminalWindowBoundsJson = preferencesService.TerminalWindowBoundsJson;
        _isInitializing = false;

        _terminalSession.OutputReceived += OnTerminalOutputReceived;
        _terminalSession.SessionExited += OnTerminalSessionExited;

        if (ShowTerminalPanel)
        {
            FireAndForget(EnsureSessionRunningAsync());
        }
    }

    public void Open() => ShowTerminalPanel = true;

    public TerminalWindowBounds? GetWindowBounds()
    {
        if (string.IsNullOrWhiteSpace(TerminalWindowBoundsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TerminalWindowBounds>(TerminalWindowBoundsJson);
        }
        catch
        {
            return null;
        }
    }

    public void UpdateWindowBounds(int x, int y, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var bounds = new TerminalWindowBounds(x, y, width, height);
        TerminalWindowBoundsJson = JsonSerializer.Serialize(bounds);
    }

    [RelayCommand]
    private void ToggleVisibility() => ShowTerminalPanel = !ShowTerminalPanel;

    [RelayCommand]
    private void Dock() => TerminalIsDocked = true;

    [RelayCommand]
    private void Undock() => TerminalIsDocked = false;

    [RelayCommand]
    private void ClearOutput()
    {
        _outputLines.Clear();
        _currentLine.Clear();
        OutputText = string.Empty;
    }

    [RelayCommand]
    private Task RestartAsync() =>
        RunSessionOperationAsync("Restarting...", _terminalSession.RestartAsync, "restart");

    [RelayCommand]
    private async Task SendInputAsync()
    {
        if (!ShowTerminalPanel)
        {
            return;
        }

        await EnsureSessionRunningAsync();

        var text = InputText;
        InputText = string.Empty;

        await _terminalSession.WriteAsync($"{text}{Environment.NewLine}");
    }

    private async Task EnsureSessionRunningAsync()
    {
        if (_terminalSession.IsRunning)
        {
            UpdateRunningState();
            return;
        }

        await RunSessionOperationAsync("Starting...", _terminalSession.StartAsync, "start");
    }

    private async Task RunSessionOperationAsync(
        string inProgressStatus,
        Func<CancellationToken, Task> operation,
        string operationName)
    {
        try
        {
            SessionStatus = inProgressStatus;
            await operation(CancellationToken.None);
            UpdateRunningState();
            OnPropertyChanged(nameof(SessionId));
        }
        catch (Exception ex)
        {
            IsRunning = false;
            SessionStatus = "Error";
            _setStatus?.Invoke($"Terminal {operationName} failed: {ex.Message}");
        }
    }

    private void UpdateRunningState()
    {
        IsRunning = _terminalSession.IsRunning;
        SessionStatus = IsRunning ? "Running" : "Stopped";
    }

    private void OnTerminalOutputReceived(object? _, string output) => RunOnUiThread(() => AppendOutput(output));

    private void OnTerminalSessionExited(object? _, int? exitCode) =>
        RunOnUiThread(() =>
        {
            IsRunning = false;
            SessionStatus = exitCode.HasValue ? $"Exited ({exitCode.Value})" : "Exited";
        });

    partial void OnShowTerminalPanelChanged(bool value)
    {
        PersistTerminalPreferences();
        if (value)
        {
            FireAndForget(EnsureSessionRunningAsync());
        }
    }

    partial void OnTerminalIsDockedChanged(bool value) => PersistTerminalPreferences();

    partial void OnTerminalDockHeightChanged(double value)
    {
        var clamped = ClampDockHeight(value);
        if (Math.Abs(clamped - value) > 0.1)
        {
            TerminalDockHeight = clamped;
            return;
        }

        PersistTerminalPreferences();
    }

    partial void OnTerminalWindowBoundsJsonChanged(string? value) => PersistTerminalPreferences();

    private void AppendOutput(string chunk)
    {
        foreach (var ch in chunk)
        {
            if (ch == '\r')
            {
                continue;
            }

            if (ch == '\n')
            {
                CommitCurrentLine();
            }
            else
            {
                _currentLine.Append(ch);
            }
        }

        var lines = _outputLines.ToList();
        if (_currentLine.Length > 0)
        {
            lines.Add(_currentLine.ToString());
        }
        OutputText = string.Join(Environment.NewLine, lines);
    }

    private void CommitCurrentLine()
    {
        _outputLines.Enqueue(_currentLine.ToString());
        _currentLine.Clear();

        while (_outputLines.Count > MaxOutputLines)
        {
            _outputLines.Dequeue();
        }
    }

    private void PersistTerminalPreferences()
    {
        if (_isInitializing)
        {
            return;
        }

        _preferencesService.ShowTerminalPanel = ShowTerminalPanel;
        _preferencesService.TerminalIsDocked = TerminalIsDocked;
        _preferencesService.TerminalDockHeight = ClampDockHeight(TerminalDockHeight);
        _preferencesService.TerminalWindowBoundsJson = TerminalWindowBoundsJson;
        _preferencesService.Save();
    }

    private static double ClampDockHeight(double value)
    {
        if (value <= 0)
        {
            return 260;
        }

        return Math.Clamp(value, MinDockHeight, MaxDockHeight);
    }

    private static void FireAndForget(Task task)
    {
        _ = task.ContinueWith(static _ => { }, TaskScheduler.Default);
    }

    private static void RunOnUiThread(Action action)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
                return;
            }

            Dispatcher.UIThread.Post(action);
        }
        catch
        {
            action();
        }
    }

    public void Dispose()
    {
        if (!TryBeginDispose())
        {
            return;
        }

        _terminalSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (!TryBeginDispose())
        {
            return;
        }

        await _terminalSession.DisposeAsync();
    }

    private bool TryBeginDispose()
    {
        if (_disposed)
        {
            return false;
        }

        _disposed = true;
        _terminalSession.OutputReceived -= OnTerminalOutputReceived;
        _terminalSession.SessionExited -= OnTerminalSessionExited;
        return true;
    }
}

public record TerminalWindowBounds(int X, int Y, double Width, double Height);
