using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using System.Collections.ObjectModel;
using SkiaSharp;

namespace BusLane.ViewModels.Dashboard;

public partial class DashboardChartViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private ObservableCollection<ISeries> _series = new();

    [ObservableProperty]
    private ObservableCollection<Axis> _xAxes = new()
    {
        new Axis
        {
            LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255)),
            Labeler = value =>
            {
                try
                {
                    return new DateTime((long)value).ToString("HH:mm");
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    };

    [ObservableProperty]
    private ObservableCollection<Axis> _yAxes = new()
    {
        new Axis
        {
            LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255)),
            MinLimit = 0
        }
    };

    [ObservableProperty]
    private string _selectedTimeRange = "1 Hour";

    [ObservableProperty]
    private bool _useGlobalTimeRange = true;

    private readonly ObservableCollection<DateTimePoint> _points = [];

    public event EventHandler<string>? TimeRangeChanged;

    public string[] TimeRangeOptions { get; } = new[]
    {
        "15 Minutes",
        "1 Hour",
        "6 Hours",
        "24 Hours"
    };

    public DashboardChartViewModel(string title)
    {
        _title = title;
        ApplyTimeRangeWindow();
    }

    partial void OnSelectedTimeRangeChanged(string value)
    {
        ApplyTimeRangeWindow();
        TimeRangeChanged?.Invoke(this, value);
    }

    public void SetGlobalTimeRange(string timeRange)
    {
        if (UseGlobalTimeRange)
        {
            SelectedTimeRange = timeRange;
        }
    }

    public void UpdateData(IEnumerable<DateTimePoint> dataPoints)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            var snapshot = dataPoints.ToList();
            Dispatcher.UIThread.Post(() => UpdateData(snapshot));
            return;
        }

        _points.Clear();
        foreach (var point in dataPoints)
        {
            _points.Add(point);
        }
        ApplyTimeRangeWindow();

        if (Series.Count == 0)
        {
            Series.Add(new LineSeries<DateTimePoint>
            {
                Values = _points,
                Fill = new SolidColorPaint(SKColors.Transparent),
                Stroke = new SolidColorPaint(GetSeriesColor()) { StrokeThickness = 2.5f },
                GeometryFill = null,
                GeometryStroke = null
            });
        }
    }

    private SKColor GetSeriesColor()
    {
        if (Title.Contains("Dead", StringComparison.OrdinalIgnoreCase))
        {
            return new SKColor(239, 83, 80);
        }

        if (Title.Contains("Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return new SKColor(255, 167, 38);
        }

        if (Title.Contains("Size", StringComparison.OrdinalIgnoreCase))
        {
            return new SKColor(102, 187, 106);
        }

        return new SKColor(66, 165, 245);
    }

    private void ApplyTimeRangeWindow()
    {
        if (XAxes.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        var min = now.Subtract(GetTimeSpan(SelectedTimeRange));

        XAxes[0].MinLimit = min.Ticks;
        XAxes[0].MaxLimit = now.Ticks;
    }

    private static TimeSpan GetTimeSpan(string selectedTimeRange)
    {
        return selectedTimeRange switch
        {
            "15 Minutes" => TimeSpan.FromMinutes(15),
            "1 Hour" => TimeSpan.FromHours(1),
            "6 Hours" => TimeSpan.FromHours(6),
            "24 Hours" => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(1)
        };
    }
}
