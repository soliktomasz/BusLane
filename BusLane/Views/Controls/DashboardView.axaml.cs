namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var scrollViewer = this.FindControl<ScrollViewer>("WidgetScrollViewer");
        scrollViewer?.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Forward all wheel events to the ScrollViewer in the tunnel phase.
        // LiveCharts controls consume wheel events for zoom/pan even when
        // ZoomMode="None", preventing the ScrollViewer from scrolling.
        // Since all dashboard charts disable zoom, this is safe.
        var scrollViewer = this.FindControl<ScrollViewer>("WidgetScrollViewer");
        if (scrollViewer is null) return;

        scrollViewer.Offset = scrollViewer.Offset.WithY(scrollViewer.Offset.Y - e.Delta.Y * 50);
        e.Handled = true;
    }
}
