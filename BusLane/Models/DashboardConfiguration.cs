namespace BusLane.Models;

public enum WidgetType
{
    LineChart,
    PieChart,
    BarChart,
    MetricCard
}

public record DashboardConfiguration
{
    public string Version { get; init; } = "1.0";
    public List<DashboardWidget> Widgets { get; init; } = [];
}

public record DashboardWidget
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public WidgetType Type { get; init; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int Width { get; set; } = 6;
    public int Height { get; set; } = 4;
    public WidgetConfiguration Configuration { get; init; } = new();
}

public record WidgetConfiguration
{
    public string? Title { get; init; }
    public string MetricName { get; init; } = "ActiveMessageCount";
    public string? EntityFilter { get; init; }
    public string TimeRange { get; init; } = "1 Hour";
    public int TopEntities { get; init; } = 10;
    public bool ShowSecondaryMetric { get; init; } = false;
}
