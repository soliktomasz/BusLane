namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

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

    protected string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Active Messages",
            "DeadLetterCount" => "Dead Letters",
            "ScheduledCount" => "Scheduled Messages",
            "SizeInBytes" => "Size (Bytes)",
            _ => Widget.Configuration.MetricName
        };
    }

    protected SKColor GetMetricColor()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => SKColors.DodgerBlue,
            "DeadLetterCount" => SKColors.OrangeRed,
            "ScheduledCount" => SKColors.Green,
            "SizeInBytes" => SKColors.Purple,
            _ => SKColors.DodgerBlue
        };
    }

    protected long GetPrimaryMetricValue(QueueInfo queue) => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => queue.ActiveMessageCount,
        "DeadLetterCount" => queue.DeadLetterCount,
        _ => queue.ActiveMessageCount
    };

    protected long GetSecondaryMetricValue(QueueInfo queue) => queue.DeadLetterCount;

    protected long GetPrimaryMetricValue(SubscriptionInfo sub) => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => sub.ActiveMessageCount,
        "DeadLetterCount" => sub.DeadLetterCount,
        _ => sub.ActiveMessageCount
    };

    protected long GetSecondaryMetricValue(SubscriptionInfo sub) => sub.DeadLetterCount;

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
