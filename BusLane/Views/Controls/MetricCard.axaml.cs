namespace BusLane.Views.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public partial class MetricCard : UserControl
{
    public static readonly StyledProperty<IBrush?> SparklineStrokeProperty =
        AvaloniaProperty.Register<MetricCard, IBrush?>(nameof(SparklineStroke));

    public static readonly StyledProperty<IBrush?> TrendForegroundProperty =
        AvaloniaProperty.Register<MetricCard, IBrush?>(nameof(TrendForeground));

    public IBrush? SparklineStroke
    {
        get => GetValue(SparklineStrokeProperty);
        set => SetValue(SparklineStrokeProperty, value);
    }

    public IBrush? TrendForeground
    {
        get => GetValue(TrendForegroundProperty);
        set => SetValue(TrendForegroundProperty, value);
    }

    public MetricCard()
    {
        InitializeComponent();
    }
}
