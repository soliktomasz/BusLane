namespace BusLane.Services.Dashboard;

using BusLane.Models;
using BusLane.Services.Infrastructure;
using System.Text.Json;

public class DashboardPersistenceService : IDashboardPersistenceService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DashboardPersistenceService(string? filePath = null)
    {
        _filePath = filePath ?? AppPaths.DashboardConfig;
    }

    public DashboardConfiguration Load()
    {
        if (!File.Exists(_filePath))
        {
            return GetDefaultConfiguration();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var config = JsonSerializer.Deserialize<DashboardConfiguration>(json);
            return config ?? GetDefaultConfiguration();
        }
        catch
        {
            var backupPath = _filePath + ".backup";
            File.Copy(_filePath, backupPath, true);
            return GetDefaultConfiguration();
        }
    }

    public void Save(DashboardConfiguration config)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public DashboardConfiguration GetDefaultConfiguration()
    {
        return new DashboardConfiguration
        {
            Widgets =
            [
                new DashboardWidget { Type = WidgetType.LineChart, Row = 0, Column = 0, Width = 12, Height = 4, Configuration = new WidgetConfiguration { Title = "Active Messages Over Time", MetricName = "ActiveMessageCount" } },
                new DashboardWidget { Type = WidgetType.LineChart, Row = 4, Column = 0, Width = 12, Height = 4, Configuration = new WidgetConfiguration { Title = "Dead Letters Over Time", MetricName = "DeadLetterCount" } },
                new DashboardWidget { Type = WidgetType.PieChart, Row = 8, Column = 0, Width = 6, Height = 4, Configuration = new WidgetConfiguration { Title = "Message Distribution", MetricName = "ActiveMessageCount", TopEntities = 10 } },
                new DashboardWidget { Type = WidgetType.BarChart, Row = 8, Column = 6, Width = 6, Height = 4, Configuration = new WidgetConfiguration { Title = "Entity Comparison", MetricName = "ActiveMessageCount", TopEntities = 10, ShowSecondaryMetric = true } }
            ]
        };
    }
}
