using BusLane.Models.Dashboard;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace BusLane.ViewModels.Dashboard;

public partial class MetricCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _unit;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private double _trendPercentage;

    [ObservableProperty]
    private MetricTrend _trend;

    [ObservableProperty]
    private double[] _sparklineData = [];

    [ObservableProperty]
    private ISeries[] _sparklineSeries = [];

    [ObservableProperty]
    private Axis[] _xAxes =
    [
        new Axis { IsVisible = false }
    ];

    [ObservableProperty]
    private Axis[] _yAxes =
    [
        new Axis { IsVisible = false }
    ];

    private readonly Queue<double> _history = new(20);
    private double? _previousValue;

    public MetricCardViewModel(string title, string unit)
    {
        _title = title;
        _unit = unit;
        _trend = MetricTrend.Stable;
        UpdateSparklineSeries();
    }

    public void UpdateValue(double newValue)
    {
        if (_previousValue.HasValue && _previousValue.Value != 0)
        {
            TrendPercentage = Math.Round(((newValue - _previousValue.Value) / _previousValue.Value) * 100, 1);
            Trend = TrendPercentage switch
            {
                > 0.1 => MetricTrend.Up,
                < -0.1 => MetricTrend.Down,
                _ => MetricTrend.Stable
            };
        }
        else
        {
            TrendPercentage = 0;
            Trend = MetricTrend.Stable;
        }

        Value = newValue;
        _previousValue = newValue;

        _history.Enqueue(newValue);
        if (_history.Count > 20)
        {
            _history.Dequeue();
        }
        SparklineData = _history.ToArray();
        UpdateSparklineSeries();
    }

    private void UpdateSparklineSeries()
    {
        SparklineSeries =
        [
            new LineSeries<double>
            {
                Values = SparklineData,
                Fill = new SolidColorPaint(SKColors.Transparent),
                Stroke = new SolidColorPaint(new SKColor(96, 205, 255)) { StrokeThickness = 2 },
                GeometryFill = null,
                GeometryStroke = null
            }
        ];
    }
}
