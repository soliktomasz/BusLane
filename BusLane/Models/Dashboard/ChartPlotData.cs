namespace BusLane.Models.Dashboard;

/// <summary>
/// A single point in a time-series line chart.
/// </summary>
public readonly record struct LinePlotPoint(DateTime Time, double Value);

/// <summary>
/// Base type for all dashboard chart payloads rendered by <c>DashboardPlotView</c>.
/// Concrete subtypes are discriminated by the renderer using pattern matching.
/// </summary>
public abstract record ChartPlotData(string Title)
{
    /// <summary>
    /// True when there is nothing meaningful to render (used to show an empty state).
    /// </summary>
    public abstract bool IsEmpty { get; }
}

/// <summary>
/// Time-series line chart. <see cref="LineColorToken"/> is a theme resource key
/// (e.g. "AccentBrand", "TextDanger") resolved by the renderer for light/dark support.
/// </summary>
public sealed record LinePlotData(
    string Title,
    IReadOnlyList<LinePlotPoint> Points,
    string LineColorToken,
    DateTime? VisibleStart = null,
    DateTime? VisibleEnd = null) : ChartPlotData(Title)
{
    public override bool IsEmpty => Points.Count < 2;
}
