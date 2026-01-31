namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Base class for all dashboard widget ViewModels. Provides grid positioning,
/// error state management, debounced refresh scheduling, and disposal.
/// </summary>
public abstract partial class DashboardWidgetViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private Timer? _refreshDebounceTimer;
    private const int RefreshDebounceMs = 100;

    [ObservableProperty] private DashboardWidget _widget;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public Guid Id => Widget.Id;
    public WidgetType Type => Widget.Type;

    public int Row
    {
        get => Widget.Row;
        set
        {
            if (Widget.Row != value)
            {
                Widget.Row = value;
                OnPropertyChanged();
            }
        }
    }

    public int Column
    {
        get => Widget.Column;
        set
        {
            if (Widget.Column != value)
            {
                Widget.Column = value;
                OnPropertyChanged();
            }
        }
    }

    public int Width
    {
        get => Widget.Width;
        set
        {
            if (Widget.Width != value)
            {
                Widget.Width = value;
                OnPropertyChanged();
            }
        }
    }

    public int Height
    {
        get => Widget.Height;
        set
        {
            if (Widget.Height != value)
            {
                Widget.Height = value;
                OnPropertyChanged();
            }
        }
    }

    protected DashboardWidgetViewModel(DashboardWidget widget)
    {
        _widget = widget;
        Title = widget.Configuration.Title ?? GetDefaultTitle();
    }

    protected abstract string GetDefaultTitle();
    public abstract void RefreshData();

    protected void ScheduleRefresh()
    {
        _refreshDebounceTimer?.Dispose();
        _refreshDebounceTimer = new Timer(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshData);
        }, null, RefreshDebounceMs, Timeout.Infinite);
    }

    protected void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    protected void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Retry()
    {
        ClearError();
        RefreshData();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshDebounceTimer?.Dispose();
            _refreshDebounceTimer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
