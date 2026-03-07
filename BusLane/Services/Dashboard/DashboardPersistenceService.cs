namespace BusLane.Services.Dashboard;

using BusLane.Models;
using BusLane.Services.Infrastructure;
using System.Text.Json;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

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
        return LoadEnvelope().Current ?? GetDefaultConfiguration();
    }

    public void Save(DashboardConfiguration config)
    {
        var envelope = LoadEnvelope();
        envelope.Current = config;
        SaveEnvelope(envelope);
    }

    public IReadOnlyList<DashboardPreset> GetPresets() => LoadEnvelope().Presets;

    public void SavePreset(DashboardPreset preset)
    {
        var envelope = LoadEnvelope();
        var presets = envelope.Presets.ToList();
        var existingIndex = presets.FindIndex(p => p.Id == preset.Id);
        if (existingIndex >= 0)
        {
            presets[existingIndex] = preset;
        }
        else
        {
            presets.Add(preset);
        }

        envelope.Presets = presets;
        SaveEnvelope(envelope);
    }

    public DashboardPreset? LoadPreset(string presetId)
    {
        return LoadEnvelope().Presets.FirstOrDefault(p => p.Id == presetId);
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

    private DashboardEnvelope LoadEnvelope()
    {
        if (!File.Exists(_filePath))
        {
            return new DashboardEnvelope
            {
                Current = GetDefaultConfiguration(),
                Presets = []
            };
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var envelope = Deserialize<DashboardEnvelope>(json);
            if (envelope?.Current != null)
            {
                return envelope;
            }

            var legacy = Deserialize<DashboardConfiguration>(json);
            return new DashboardEnvelope
            {
                Current = legacy ?? GetDefaultConfiguration(),
                Presets = []
            };
        }
        catch
        {
            var backupPath = _filePath + ".backup";
            File.Copy(_filePath, backupPath, true);
            return new DashboardEnvelope
            {
                Current = GetDefaultConfiguration(),
                Presets = []
            };
        }
    }

    private void SaveEnvelope(DashboardEnvelope envelope)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = Serialize(envelope);
        File.WriteAllText(_filePath, json);
    }

    private sealed record DashboardEnvelope
    {
        public DashboardConfiguration? Current { get; set; }
        public List<DashboardPreset> Presets { get; set; } = [];
    }
}
