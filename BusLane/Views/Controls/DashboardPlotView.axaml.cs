namespace BusLane.Views.Controls;

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using BusLane.Models.Dashboard;
using ScottPlot;
using ScottPlot.TickGenerators;
using SkiaSharp;

/// <summary>
/// Reusable ScottPlot-backed chart that renders a <see cref="ChartPlotData"/> payload.
/// Used by both the monitoring namespace dashboard and the custom widget dashboard.
/// Charts read theme tokens (e.g. "AccentBrand") so they stay consistent across light/dark.
/// </summary>
public partial class DashboardPlotView : UserControl
{
    public static readonly StyledProperty<object?> DataProperty =
        AvaloniaProperty.Register<DashboardPlotView, object?>(nameof(Data));

    /// <summary>
    /// The chart payload to render. Expected to be a <see cref="ChartPlotData"/> subtype.
    /// </summary>
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private bool _loaded;

    public DashboardPlotView()
    {
        InitializeComponent();

        // Read-only monitoring charts: keep the outer ScrollViewer in control of the wheel.
        PlotHost.UserInputProcessor.Disable();
        ActualThemeVariantChanged += (_, _) => { if (_loaded) Render(); };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _loaded = true;
        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty && _loaded)
        {
            Render();
        }
    }

    private void Render()
    {
        var data = Data as ChartPlotData;
        PlotHost.Plot.Clear();

        if (data is null || data.IsEmpty)
        {
            EmptyStateText.Text = GetEmptyStateText(data);
            PlotHost.IsVisible = false;
            EmptyState.IsVisible = true;
            return;
        }

        EmptyState.IsVisible = false;
        PlotHost.IsVisible = true;

        switch (data)
        {
            case LinePlotData line: RenderLine(line); break;
            case BarPlotData bar: RenderBar(bar); break;
            case PiePlotData pie: RenderPie(pie); break;
        }

        PlotHost.Refresh();
    }

    private void RenderLine(LinePlotData line)
    {
        var points = line.Points.OrderBy(p => p.Time).ToList();
        if (points.Count == 0)
        {
            return;
        }

        var xs = points.Select(p => p.Time.ToOADate()).ToArray();
        var ys = points.Select(p => p.Value).ToArray();
        var color = ResolveColor(line.LineColorToken);

        if (points.Count == 1)
        {
            PlotHost.Plot.Add.Scatter(xs[0], ys[0], color);
        }
        else
        {
            PlotHost.Plot.Add.Scatter(xs, ys, color);
        }

        PlotHost.Plot.Axes.DateTimeTicksBottom();

        var horizontalLimits = GetHorizontalLimits(line, xs);
        var xMin = horizontalLimits.Minimum;
        var xMax = horizontalLimits.Maximum;
        var yMax = Math.Max(ys.Max(), 1);
        var xSpan = xMax - xMin is > 0 ? xMax - xMin : 1;
        var xPadding = horizontalLimits.UsesVisibleWindow ? 0 : xSpan * 0.04;
        PlotHost.Plot.Axes.SetLimits(xMin - xPadding, xMax + xPadding, 0, yMax * 1.15);

        StyleAxes(PlotHost.Plot);
    }

    internal static string GetEmptyStateText(ChartPlotData? data)
    {
        return data is LinePlotData line && line.Points.Count == 1
            ? "Collecting history"
            : "No data yet";
    }

    internal static (double Minimum, double Maximum, bool UsesVisibleWindow) GetHorizontalLimits(
        LinePlotData line,
        IReadOnlyList<double> xValues)
    {
        var visibleStart = line.VisibleStart;
        var visibleEnd = line.VisibleEnd;
        var usesVisibleWindow = visibleStart.HasValue
            && visibleEnd.HasValue
            && visibleEnd.Value > visibleStart.Value;

        return usesVisibleWindow
            ? (visibleStart!.Value.ToOADate(), visibleEnd!.Value.ToOADate(), true)
            : (xValues.Min(), xValues.Max(), false);
    }

    private void RenderBar(BarPlotData bar)
    {
        var categories = bar.Categories;
        var count = categories.Count;
        var seriesCount = bar.Series.Count;
        var width = Math.Max(0.22, 0.72 / Math.Max(1, seriesCount));

        var bars = new List<ScottPlot.Bar>();
        for (var s = 0; s < seriesCount; s++)
        {
            var series = bar.Series[s];
            var color = ResolveColor(series.FillColorToken);
            var offset = (s - (seriesCount - 1) / 2.0) * width;
            for (var i = 0; i < count; i++)
            {
                bars.Add(new ScottPlot.Bar
                {
                    Position = i + offset,
                    Value = series.Values[i],
                    Size = width,
                    FillColor = color
                });
            }
        }

        PlotHost.Plot.Add.Bars(bars);

        var positions = Enumerable.Range(0, count).Select(i => (double)i).ToArray();
        PlotHost.Plot.Axes.Bottom.TickGenerator = new NumericManual(positions, categories.ToArray());

        var max = bars.Select(b => b.Value).DefaultIfEmpty(0).Max();
        if (max <= 0)
        {
            max = 1;
        }
        PlotHost.Plot.Axes.SetLimits(-0.5, count - 0.5, 0, max * 1.2);

        StyleAxes(PlotHost.Plot);
        PlotHost.Plot.Axes.Bottom.TickLabelStyle.Rotation = -30;
    }

    private void RenderPie(PiePlotData pie)
    {
        var slices = pie.Slices
            .Select(s => new PieSlice
            {
                Value = s.Value,
                Label = s.Name,
                LegendText = s.Name,
                FillColor = ResolveColor(s.FillColorToken)
            })
            .ToList();

        PlotHost.Plot.Add.Pie(slices);
        PlotHost.Plot.FigureBackground.Color = ColorHex("#00000000");
        PlotHost.Plot.DataBackground.Color = ColorHex("#00000000");
        PlotHost.Plot.HideAxesAndGrid();

        PlotHost.Plot.Legend.IsVisible = true;
        PlotHost.Plot.Legend.Alignment = Alignment.UpperRight;
        PlotHost.Plot.Legend.FontSize = 10;
        PlotHost.Plot.Legend.FontColor = ResolveColor("SubtleForeground");
        PlotHost.Plot.Legend.BackgroundColor = ColorHex("#00000000");
        PlotHost.Plot.Legend.OutlineWidth = 0;
    }

    /// <summary>
    /// Applies the Calm Operator chart treatment: transparent background so the card shows
    /// through, muted axes, and subtle horizontal gridlines only.
    /// </summary>
    private void StyleAxes(Plot plot)
    {
        var axisColor = ResolveColor("SubtleForeground");
        var gridColor = ResolveColor("BorderDefault");

        plot.FigureBackground.Color = ColorHex("#00000000");
        plot.DataBackground.Color = ColorHex("#00000000");
        // Borderless plot area: the hosting card owns the frame.
        plot.FigureBorder.Color = ColorHex("#00000000");
        plot.DataBorder.Color = ColorHex("#00000000");
        plot.Axes.Color(axisColor);

        // Horizontal gridlines only, quiet.
        plot.Grid.IsVisible = true;
        plot.Grid.XAxisStyle.IsVisible = false;
        plot.Grid.YAxisStyle.IsVisible = true;
        plot.Grid.MajorLineColor = gridColor;
        plot.Grid.MinorLineColor = ColorHex("#00000000");
        plot.Grid.MajorLineWidth = 1;
        plot.Grid.IsBeneathPlottables = true;

        plot.Axes.Bottom.TickLabelStyle.FontSize = 10;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = axisColor;
        plot.Axes.Left.TickLabelStyle.FontSize = 10;
        plot.Axes.Left.TickLabelStyle.ForeColor = axisColor;
    }

    /// <summary>
    /// Resolves a theme resource token (e.g. "AccentBrand") to a ScottPlot color so the chart
    /// follows the active light/dark theme and stays consistent with the rest of the UI.
    /// </summary>
    private ScottPlot.Color ResolveColor(string token)
    {
        try
        {
            if (App.Current?.Resources.TryGetResource(token, App.Current.ActualThemeVariant, out var resource) == true
                && resource is ISolidColorBrush brush)
            {
                var color = brush.Color;
                return ScottPlot.Color.FromSKColor(new SKColor(color.R, color.G, color.B, color.A));
            }
        }
        catch
        {
            // Fall through to the brand default.
        }

        return ScottPlot.Color.FromHex("#4F46E5");
    }

    private static ScottPlot.Color ColorHex(string hex) => ScottPlot.Color.FromHex(hex);
}
