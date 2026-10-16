namespace BusLane.Views.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public partial class MetricCard : UserControl
{
    public static readonly StyledProperty<IBrush?> SparklineStrokeProperty =
        AvaloniaProperty.Register<MetricCard, IBrush?>(nameof(SparklineStroke));

    public IBrush? SparklineStroke
    {
        get => GetValue(SparklineStrokeProperty);
        set => SetValue(SparklineStrokeProperty, value);
    }

    public MetricCard()
    {
        InitializeComponent();
    }
}
