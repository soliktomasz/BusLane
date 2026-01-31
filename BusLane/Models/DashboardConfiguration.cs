namespace BusLane.Models;

/// <summary>Available dashboard widget types.</summary>
public enum WidgetType
{
    LineChart,
    PieChart,
    BarChart,
    MetricCard
}

/// <summary>Top-level dashboard configuration containing all widget definitions.</summary>
public record DashboardConfiguration
{
    public string Version { get; init; } = "1.0";
    public List<DashboardWidget> Widgets { get; init; } = [];
}

/// <summary>Defines a widget's type, grid position, size, and metric configuration.</summary>
public class DashboardWidget
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public WidgetType Type { get; init; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int Width { get; set; } = 6;
    public int Height { get; set; } = 4;
    public WidgetConfiguration Configuration { get; init; } = new();
}

/// <summary>Per-widget settings controlling which metric, entity, and time range to display.</summary>
public record WidgetConfiguration
{
    public string? Title { get; init; }
    public string MetricName { get; init; } = "ActiveMessageCount";
    public string? EntityFilter { get; init; }
    public string TimeRange { get; init; } = "1 Hour";
    public int TopEntities { get; init; } = 10;
    public bool ShowSecondaryMetric { get; init; } = false;
}
