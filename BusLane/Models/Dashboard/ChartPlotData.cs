namespace BusLane.Models.Dashboard;

/// <summary>
/// A single point in a time-series line chart.
/// </summary>
public readonly record struct LinePlotPoint(DateTime Time, double Value);

/// <summary>
/// A single group of bars (one series) aligned to <see cref="BarPlotData.Categories"/>.
/// </summary>
public sealed record BarPlotSeries(string Name, double[] Values, string FillColorToken);

/// <summary>
/// One slice in a pie/donut chart.
/// </summary>
public sealed record PiePlotSlice(string Name, double Value, string FillColorToken);

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

/// <summary>
/// Grouped bar chart (e.g. active vs dead-letter per entity).
/// Each series aligns to <see cref="Categories"/> by index.
/// </summary>
public sealed record BarPlotData(
    string Title,
    IReadOnlyList<string> Categories,
    IReadOnlyList<BarPlotSeries> Series) : ChartPlotData(Title)
{
    public override bool IsEmpty => Categories.Count == 0 || Series.Count == 0;
}

/// <summary>
/// Pie / donut distribution chart.
/// </summary>
public sealed record PiePlotData(
    string Title,
    IReadOnlyList<PiePlotSlice> Slices) : ChartPlotData(Title)
{
    public override bool IsEmpty => Slices.Count == 0;
}
