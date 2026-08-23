namespace BusLane.Views.Controls;

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

/// <summary>
/// Lightweight, dependency-free sparkline for compact metric-card surfaces.
/// Draws a quiet accent line (and a faint area fill) scaled to its bounds.
/// </summary>
public class SparklineView : Control
{
    public static readonly StyledProperty<IEnumerable<double>?> ValuesProperty =
        AvaloniaProperty.Register<SparklineView, IEnumerable<double>?>(nameof(Values));

    /// <summary>The numeric values to draw, left to right.</summary>
    public IEnumerable<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<SparklineView, IBrush?>(nameof(Stroke));

    /// <summary>Line color. Defaults to the brand accent so it reads in both themes.</summary>
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    static SparklineView()
    {
        AffectsRender<SparklineView>(ValuesProperty, StrokeProperty);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values?.Where(double.IsFinite).ToArray();
        if (values is null || values.Length < 2 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var stroke = Stroke ?? new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));
        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        if (range <= 0)
        {
            range = 1;
        }

        var left = 1.5;
        var right = Bounds.Width - 1.5;
        var top = 1.5;
        var bottom = Bounds.Height - 1.5;
        var width = right - left;
        var height = bottom - top;

        var points = new Point[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var x = left + (i / (double)(values.Length - 1)) * width;
            var y = bottom - ((values[i] - min) / range) * height;
            points[i] = new Point(x, y);
        }

        // Faint area fill beneath the line (Calm Operator: quiet, low contrast).
        var fill = new StreamGeometry();
        using (var fctx = fill.Open())
        {
            fctx.BeginFigure(new Point(points[0].X, bottom), true);
            fctx.LineTo(points[0]);
            for (var i = 1; i < points.Length; i++)
            {
                fctx.LineTo(points[i]);
            }
            fctx.LineTo(new Point(points[^1].X, bottom));
            fctx.EndFigure(true);
        }
        var fillBrush = stroke is SolidColorBrush fg
            ? new SolidColorBrush(fg.Color, 0.12)
            : new SolidColorBrush(Colors.Transparent);
        context.DrawGeometry(fillBrush, null, fill);

        // The line itself.
        var line = new StreamGeometry();
        using (var lctx = line.Open())
        {
            lctx.BeginFigure(points[0], false);
            for (var i = 1; i < points.Length; i++)
            {
                lctx.LineTo(points[i]);
            }
            lctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(stroke, 1.6), line);
    }
}
