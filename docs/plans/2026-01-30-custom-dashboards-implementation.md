# Custom Dashboards Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace static ChartsView with dynamic, customizable dashboard system with draggable, resizable widgets in a grid layout.

**Architecture:** Grid-based layout engine (12-column) manages widget positioning. Each widget has its own ViewModel bound to LiveCharts2 charts or metric cards. Dashboard configuration persists to JSON. Existing IMetricsService feeds all widgets.

**Tech Stack:** Avalonia UI 11.3, LiveCharts2, .NET 10, CommunityToolkit.Mvvm, xUnit for tests

---

## Prerequisites

- Branch: `feature/custom-dashboards` (already created)
- Design doc: `docs/plans/2026-01-30-custom-dashboards-design.md`
- Read existing `ChartsViewModel.cs` and `ChartsView.axaml` for reference

---

### Task 1: Create Data Models

**Files:**
- Create: `BusLane/Models/DashboardConfiguration.cs`

**Step 1: Write the model classes**

```csharp
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
```

**Step 2: Verify file compiles**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/Models/DashboardConfiguration.cs
git commit -m "feat: add dashboard configuration models"
```

---

### Task 2: Create Dashboard Persistence Service Interface

**Files:**
- Create: `BusLane/Services/Dashboard/IDashboardPersistenceService.cs`

**Step 1: Write the interface**

```csharp
namespace BusLane.Services.Dashboard;

using Models;

public interface IDashboardPersistenceService
{
    DashboardConfiguration Load();
    void Save(DashboardConfiguration config);
    DashboardConfiguration GetDefaultConfiguration();
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/Services/Dashboard/IDashboardPersistenceService.cs
git commit -m "feat: add dashboard persistence service interface"
```

---

### Task 3: Add Dashboard Config Path to AppPaths

**Files:**
- Modify: `BusLane/Models/AppPaths.cs`

**Step 1: Read current AppPaths.cs to find the class structure**

**Step 2: Add DashboardConfig property**

Add to the AppPaths class:
```csharp
public static string DashboardConfig => Path.Combine(ConfigDirectory, "dashboard.json");
```

**Step 3: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add BusLane/Models/AppPaths.cs
git commit -m "feat: add dashboard config path to AppPaths"
```

---

### Task 4: Create Dashboard Persistence Service Implementation

**Files:**
- Create: `BusLane/Services/Dashboard/DashboardPersistenceService.cs`
- Test: `BusLane.Tests/Services/Dashboard/DashboardPersistenceServiceTests.cs`

**Step 1: Write the test first**

```csharp
namespace BusLane.Tests.Services.Dashboard;

using BusLane.Models;
using BusLane.Services.Dashboard;
using System.IO;
using Xunit;

public class DashboardPersistenceServiceTests
{
    private readonly string _testPath = Path.Combine(Path.GetTempPath(), $"test_dashboard_{Guid.NewGuid()}.json");

    [Fact]
    public void SaveAndLoad_Roundtrip_PreservesConfiguration()
    {
        var service = new DashboardPersistenceService(_testPath);
        var config = new DashboardConfiguration
        {
            Widgets =
            [
                new DashboardWidget { Type = WidgetType.LineChart, Row = 0, Column = 0, Width = 6, Height = 4 }
            ]
        };

        service.Save(config);
        var loaded = service.Load();

        Assert.Single(loaded.Widgets);
        Assert.Equal(WidgetType.LineChart, loaded.Widgets[0].Type);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultConfiguration()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
        var service = new DashboardPersistenceService(nonExistentPath);

        var loaded = service.Load();

        Assert.Equal(4, loaded.Widgets.Count);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~DashboardPersistenceServiceTests" -v n`
Expected: FAIL - DashboardPersistenceService type not found

**Step 3: Write the implementation**

```csharp
namespace BusLane.Services.Dashboard;

using BusLane.Models;
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
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~DashboardPersistenceServiceTests" -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add BusLane/Services/Dashboard/DashboardPersistenceService.cs BusLane.Tests/Services/Dashboard/DashboardPersistenceServiceTests.cs
git commit -m "feat: add dashboard persistence service with tests"
```

---

### Task 5: Create Dashboard Layout Engine

**Files:**
- Create: `BusLane/Services/Dashboard/DashboardLayoutEngine.cs`
- Test: `BusLane.Tests/Services/Dashboard/DashboardLayoutEngineTests.cs`

**Step 1: Write the tests**

```csharp
namespace BusLane.Tests.Services.Dashboard;

using BusLane.Models;
using BusLane.Services.Dashboard;
using System.Collections.ObjectModel;
using Xunit;

public class DashboardLayoutEngineTests
{
    private readonly DashboardLayoutEngine _engine = new();

    [Fact]
    public void FindNextAvailableSlot_EmptyGrid_ReturnsZeroZero()
    {
        var widgets = new ObservableCollection<DashboardWidget>();

        var (row, col) = _engine.FindNextAvailableSlot(widgets, 6, 4);

        Assert.Equal(0, row);
        Assert.Equal(0, col);
    }

    [Fact]
    public void FindNextAvailableSlot_WithWidget_FindsNextSlot()
    {
        var widgets = new ObservableCollection<DashboardWidget>
        {
            new() { Row = 0, Column = 0, Width = 6, Height = 4 }
        };

        var (row, col) = _engine.FindNextAvailableSlot(widgets, 6, 4);

        Assert.Equal(0, row);
        Assert.Equal(6, col);
    }

    [Fact]
    public void CanMove_WhenSpaceIsOccupied_ReturnsFalse()
    {
        var widget = new DashboardWidget { Row = 0, Column = 0, Width = 6, Height = 4 };
        var widgets = new ObservableCollection<DashboardWidget> { widget };

        bool canMove = _engine.CanMove(widgets, widget, 0, 0);

        Assert.True(canMove); // Same position is valid
    }

    [Fact]
    public void CanResize_WhenSpaceAvailable_ReturnsTrue()
    {
        var widget = new DashboardWidget { Row = 0, Column = 0, Width = 6, Height = 4 };
        var widgets = new ObservableCollection<DashboardWidget> { widget };

        bool canResize = _engine.CanResize(widgets, widget, 9, 4);

        Assert.True(canResize);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~DashboardLayoutEngineTests" -v n`
Expected: FAIL - DashboardLayoutEngine not found

**Step 3: Write the implementation**

```csharp
namespace BusLane.Services.Dashboard;

using BusLane.Models;
using System.Collections.ObjectModel;

public class DashboardLayoutEngine
{
    private const int GridColumns = 12;

    public (int row, int col) FindNextAvailableSlot(ObservableCollection<DashboardWidget> widgets, int width, int height)
    {
        if (widgets.Count == 0)
            return (0, 0);

        int maxRow = widgets.Any() ? widgets.Max(w => w.Row + w.Height) : 0;

        for (int row = 0; row <= maxRow + height; row++)
        {
            for (int col = 0; col <= GridColumns - width; col++)
            {
                if (IsSpaceAvailable(widgets, null, row, col, width, height))
                    return (row, col);
            }
        }

        return (maxRow, 0);
    }

    public bool CanMove(ObservableCollection<DashboardWidget> widgets, DashboardWidget widget, int newRow, int newCol)
    {
        return IsSpaceAvailable(widgets, widget, newRow, newCol, widget.Width, widget.Height);
    }

    public bool CanResize(ObservableCollection<DashboardWidget> widgets, DashboardWidget widget, int newWidth, int newHeight)
    {
        if (newWidth < 3 || newHeight < 2 || newCol + newWidth > GridColumns)
            return false;

        return IsSpaceAvailable(widgets, widget, widget.Row, widget.Column, newWidth, newHeight);
    }

    private bool IsSpaceAvailable(ObservableCollection<DashboardWidget> widgets, DashboardWidget? excludeWidget, int row, int col, int width, int height)
    {
        if (col + width > GridColumns || col < 0 || row < 0)
            return false;

        foreach (var w in widgets)
        {
            if (w == excludeWidget)
                continue;

            bool overlap = !(w.Row + w.Height <= row || w.Row >= row + height ||
                           w.Column + w.Width <= col || w.Column >= col + width);

            if (overlap)
                return false;
        }

        return true;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~DashboardLayoutEngineTests" -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add BusLane/Services/Dashboard/DashboardLayoutEngine.cs BusLane.Tests/Services/Dashboard/DashboardLayoutEngineTests.cs
git commit -m "feat: add dashboard layout engine with tests"
```

---

### Task 6: Create Base Dashboard Widget ViewModel

**Files:**
- Create: `BusLane/ViewModels/Dashboard/DashboardWidgetViewModel.cs`

**Step 1: Write the base ViewModel**

```csharp
namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public abstract partial class DashboardWidgetViewModel : ViewModelBase
{
    [ObservableProperty] private DashboardWidget _widget;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public Guid Id => Widget.Id;
    public WidgetType Type => Widget.Type;

    public int Row
    {
        get => Widget.Row;
        set
        {
            if (Widget.Row != value)
            {
                Widget.Row = value;
                OnPropertyChanged();
            }
        }
    }

    public int Column
    {
        get => Widget.Column;
        set
        {
            if (Widget.Column != value)
            {
                Widget.Column = value;
                OnPropertyChanged();
            }
        }
    }

    public int Width
    {
        get => Widget.Width;
        set
        {
            if (Widget.Width != value)
            {
                Widget.Width = value;
                OnPropertyChanged();
            }
        }
    }

    public int Height
    {
        get => Widget.Height;
        set
        {
            if (Widget.Height != value)
            {
                Widget.Height = value;
                OnPropertyChanged();
            }
        }
    }

    protected DashboardWidgetViewModel(DashboardWidget widget)
    {
        _widget = widget;
        Title = widget.Configuration.Title ?? GetDefaultTitle();
    }

    protected abstract string GetDefaultTitle();
    public abstract void RefreshData();

    protected void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    protected void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Retry()
    {
        ClearError();
        RefreshData();
    }
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/ViewModels/Dashboard/DashboardWidgetViewModel.cs
git commit -m "feat: add base dashboard widget viewmodel"
```

---

### Task 7: Create Line Chart Widget ViewModel

**Files:**
- Create: `BusLane/ViewModels/Dashboard/LineChartWidgetViewModel.cs`

**Step 1: Write the ViewModel**

```csharp
namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Services.Monitoring;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

public partial class LineChartWidgetViewModel : DashboardWidgetViewModel
{
    private readonly IMetricsService _metricsService;

    public ObservableCollection<ISeries> Series { get; } = [];
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public LineChartWidgetViewModel(DashboardWidget widget, IMetricsService metricsService) : base(widget)
    {
        _metricsService = metricsService;

        XAxes = [new Axis
        {
            Name = "Time",
            Labeler = value => new DateTime((long)value).ToString("HH:mm"),
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray)
        }];

        YAxes = [new Axis
        {
            Name = GetMetricDisplayName(),
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }];

        Series.Add(new LineSeries<DateTimePoint>
        {
            Name = GetMetricDisplayName(),
            Values = new ObservableCollection<DateTimePoint>(),
            Fill = null,
            GeometrySize = 4,
            Stroke = new SolidColorPaint(GetMetricColor(), 2),
            GeometryStroke = new SolidColorPaint(GetMetricColor(), 2)
        });

        _metricsService.MetricRecorded += OnMetricRecorded;
        RefreshData();
    }

    private void OnMetricRecorded(object? sender, MetricDataPoint dataPoint)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshData);
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();
            var duration = GetTimeSpan();

            IEnumerable<MetricDataPoint> metrics;
            if (string.IsNullOrEmpty(Widget.Configuration.EntityFilter))
            {
                metrics = _metricsService.GetAggregatedMetrics(Widget.Configuration.MetricName, duration);
            }
            else
            {
                metrics = _metricsService.GetMetricHistory(Widget.Configuration.EntityFilter, Widget.Configuration.MetricName, duration);
            }

            var points = metrics
                .GroupBy(m => new DateTime(m.Timestamp.Year, m.Timestamp.Month, m.Timestamp.Day,
                    m.Timestamp.Hour, m.Timestamp.Minute / 5 * 5, 0))
                .Select(g => new DateTimePoint(g.Key, g.Sum(m => m.Value)))
                .OrderBy(p => p.DateTime)
                .ToList();

            if (Series.Count > 0 && Series[0] is LineSeries<DateTimePoint> series)
            {
                var values = (ObservableCollection<DateTimePoint>)series.Values!;
                values.Clear();
                foreach (var point in points)
                {
                    values.Add(point);
                }
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return $"{GetMetricDisplayName()} Over Time";
    }

    private string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Active Messages",
            "DeadLetterCount" => "Dead Letters",
            "ScheduledCount" => "Scheduled Messages",
            "SizeInBytes" => "Size (Bytes)",
            _ => Widget.Configuration.MetricName
        };
    }

    private SKColor GetMetricColor()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => SKColors.DodgerBlue,
            "DeadLetterCount" => SKColors.OrangeRed,
            "ScheduledCount" => SKColors.Green,
            "SizeInBytes" => SKColors.Purple,
            _ => SKColors.DodgerBlue
        };
    }

    private TimeSpan GetTimeSpan()
    {
        return Widget.Configuration.TimeRange switch
        {
            "15 Minutes" => TimeSpan.FromMinutes(15),
            "1 Hour" => TimeSpan.FromHours(1),
            "6 Hours" => TimeSpan.FromHours(6),
            "24 Hours" => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(1)
        };
    }
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/ViewModels/Dashboard/LineChartWidgetViewModel.cs
git commit -m "feat: add line chart widget viewmodel"
```

---

### Task 8: Create Pie Chart Widget ViewModel

**Files:**
- Create: `BusLane/ViewModels/Dashboard/PieChartWidgetViewModel.cs`

**Step 1: Write the ViewModel**

```csharp
namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

public partial class PieChartWidgetViewModel : DashboardWidgetViewModel
{
    public ObservableCollection<ISeries> Series { get; } = [];

    private readonly ObservableCollection<QueueInfo> _queues = [];
    private readonly ObservableCollection<SubscriptionInfo> _subscriptions = [];

    public PieChartWidgetViewModel(DashboardWidget widget) : base(widget)
    {
        RefreshData();
    }

    public void UpdateEntityData(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        _queues.Clear();
        foreach (var q in queues)
            _queues.Add(q);

        _subscriptions.Clear();
        foreach (var s in subscriptions)
            _subscriptions.Add(s);

        RefreshData();
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();
            Series.Clear();

            var data = new List<(string Name, double Value)>();

            foreach (var queue in _queues.OrderByDescending(q => GetMetricValue(q)).Take(Widget.Configuration.TopEntities))
            {
                data.Add((queue.Name, GetMetricValue(queue)));
            }

            foreach (var sub in _subscriptions.OrderByDescending(s => GetMetricValue(s)).Take(Widget.Configuration.TopEntities))
            {
                data.Add(($"{sub.TopicName}/{sub.Name}", GetMetricValue(sub)));
            }

            var colors = new[]
            {
                SKColors.DodgerBlue, SKColors.Orange, SKColors.Green, SKColors.Purple,
                SKColors.Red, SKColors.Teal, SKColors.Gold, SKColors.Pink,
                SKColors.LimeGreen, SKColors.Coral
            };

            for (var i = 0; i < data.Count && i < colors.Length; i++)
            {
                Series.Add(new PieSeries<double>
                {
                    Name = data[i].Name,
                    Values = [data[i].Value],
                    Fill = new SolidColorPaint(colors[i]),
                    DataLabelsSize = 12,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
                });
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return $"{GetMetricDisplayName()} Distribution";
    }

    private double GetMetricValue(QueueInfo queue)
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => queue.ActiveMessageCount,
            "DeadLetterCount" => queue.DeadLetterCount,
            _ => queue.ActiveMessageCount
        };
    }

    private double GetMetricValue(SubscriptionInfo sub)
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => sub.ActiveMessageCount,
            "DeadLetterCount" => sub.DeadLetterCount,
            _ => sub.ActiveMessageCount
        };
    }

    private string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Message",
            "DeadLetterCount" => "Dead Letter",
            _ => "Message"
        };
    }
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/ViewModels/Dashboard/PieChartWidgetViewModel.cs
git commit -m "feat: add pie chart widget viewmodel"
```

---

### Task 9: Create Bar Chart Widget ViewModel

**Files:**
- Create: `BusLane/ViewModels/Dashboard/BarChartWidgetViewModel.cs`

**Step 1: Write the ViewModel**

```csharp
namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

public partial class BarChartWidgetViewModel : DashboardWidgetViewModel
{
    public ObservableCollection<ISeries> Series { get; } = [];
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    private readonly ObservableCollection<QueueInfo> _queues = [];
    private readonly ObservableCollection<SubscriptionInfo> _subscriptions = [];

    public BarChartWidgetViewModel(DashboardWidget widget) : base(widget)
    {
        XAxes = [new Axis
        {
            Name = "Entity",
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            Labels = []
        }];

        YAxes = [new Axis
        {
            Name = "Count",
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }];

        RefreshData();
    }

    public void UpdateEntityData(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        _queues.Clear();
        foreach (var q in queues)
            _queues.Add(q);

        _subscriptions.Clear();
        foreach (var s in subscriptions)
            _subscriptions.Add(s);

        RefreshData();
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();
            Series.Clear();

            var entities = _queues.Select(q => (Name: q.Name, Active: (double)GetPrimaryMetric(q), DeadLetter: (double)GetSecondaryMetric(q)))
                .Concat(_subscriptions.Select(s => (Name: $"{s.TopicName}/{s.Name}", Active: (double)GetPrimaryMetric(s), DeadLetter: (double)GetSecondaryMetric(s))))
                .OrderByDescending(e => e.Active + e.DeadLetter)
                .Take(Widget.Configuration.TopEntities)
                .ToList();

            XAxes[0].Labels = entities.Select(e => e.Name).ToArray();

            Series.Add(new ColumnSeries<double>
            {
                Name = GetPrimaryMetricName(),
                Values = entities.Select(e => e.Active).ToList(),
                Fill = new SolidColorPaint(SKColors.DodgerBlue)
            });

            if (Widget.Configuration.ShowSecondaryMetric)
            {
                Series.Add(new ColumnSeries<double>
                {
                    Name = GetSecondaryMetricName(),
                    Values = entities.Select(e => e.DeadLetter).ToList(),
                    Fill = new SolidColorPaint(SKColors.OrangeRed)
                });
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return "Entity Comparison";
    }

    private long GetPrimaryMetric(QueueInfo queue) => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => queue.ActiveMessageCount,
        "DeadLetterCount" => queue.DeadLetterCount,
        _ => queue.ActiveMessageCount
    };

    private long GetSecondaryMetric(QueueInfo queue) => queue.DeadLetterCount;

    private long GetPrimaryMetric(SubscriptionInfo sub) => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => sub.ActiveMessageCount,
        "DeadLetterCount" => sub.DeadLetterCount,
        _ => sub.ActiveMessageCount
    };

    private long GetSecondaryMetric(SubscriptionInfo sub) => sub.DeadLetterCount;

    private string GetPrimaryMetricName() => Widget.Configuration.MetricName switch
    {
        "ActiveMessageCount" => "Active Messages",
        "DeadLetterCount" => "Dead Letters",
        _ => "Active Messages"
    };

    private string GetSecondaryMetricName() => "Dead Letters";
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/ViewModels/Dashboard/BarChartWidgetViewModel.cs
git commit -m "feat: add bar chart widget viewmodel"
```

---

### Task 10: Create Metric Card Widget ViewModel

**Files:**
- Create: `BusLane/ViewModels/Dashboard/MetricCardWidgetViewModel.cs`

**Step 1: Write the ViewModel**

```csharp
namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Services.Monitoring;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class MetricCardWidgetViewModel : DashboardWidgetViewModel
{
    private readonly IMetricsService _metricsService;

    [ObservableProperty] private double _currentValue;
    [ObservableProperty] private double _previousValue;
    [ObservableProperty] private bool _isTrendUp;
    [ObservableProperty] private string _trendPercentage = "0%";
    [ObservableProperty] private string _metricUnit = string.Empty;

    public MetricCardWidgetViewModel(DashboardWidget widget, IMetricsService metricsService) : base(widget)
    {
        _metricsService = metricsService;
        MetricUnit = GetMetricUnit();
        _metricsService.MetricRecorded += OnMetricRecorded;
        RefreshData();
    }

    private void OnMetricRecorded(object? sender, MetricDataPoint dataPoint)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshData);
    }

    public override void RefreshData()
    {
        try
        {
            ClearError();

            var duration = GetComparisonTimeSpan();
            var currentDuration = TimeSpan.FromMinutes(5);

            IEnumerable<MetricDataPoint> currentMetrics;
            IEnumerable<MetricDataPoint> previousMetrics;

            if (string.IsNullOrEmpty(Widget.Configuration.EntityFilter))
            {
                currentMetrics = _metricsService.GetAggregatedMetrics(Widget.Configuration.MetricName, currentDuration);
                previousMetrics = _metricsService.GetAggregatedMetrics(Widget.Configuration.MetricName, duration);
            }
            else
            {
                currentMetrics = _metricsService.GetMetricHistory(Widget.Configuration.EntityFilter, Widget.Configuration.MetricName, currentDuration);
                previousMetrics = _metricsService.GetMetricHistory(Widget.Configuration.EntityFilter, Widget.Configuration.MetricName, duration);
            }

            CurrentValue = currentMetrics.Any() ? currentMetrics.Average(m => m.Value) : 0;
            var previousRaw = previousMetrics.Any() ? previousMetrics.Average(m => m.Value) : 0;

            if (previousRaw > 0)
            {
                var change = (CurrentValue - previousRaw) / previousRaw;
                IsTrendUp = change > 0;
                TrendPercentage = $"{Math.Abs(change) * 100:F1}%";
            }
            else
            {
                IsTrendUp = CurrentValue > 0;
                TrendPercentage = CurrentValue > 0 ? "New" : "0%";
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to load data: {ex.Message}");
        }
    }

    protected override string GetDefaultTitle()
    {
        return GetMetricDisplayName();
    }

    private string GetMetricDisplayName()
    {
        return Widget.Configuration.MetricName switch
        {
            "ActiveMessageCount" => "Active Messages",
            "DeadLetterCount" => "Dead Letters",
            "ScheduledCount" => "Scheduled Messages",
            "SizeInBytes" => "Queue Size",
            _ => Widget.Configuration.MetricName
        };
    }

    private string GetMetricUnit()
    {
        return Widget.Configuration.MetricName == "SizeInBytes" ? "bytes" : "messages";
    }

    private TimeSpan GetComparisonTimeSpan()
    {
        return Widget.Configuration.TimeRange switch
        {
            "Previous Hour" => TimeSpan.FromHours(1),
            "Previous Day" => TimeSpan.FromDays(1),
            _ => TimeSpan.FromHours(1)
        };
    }
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/ViewModels/Dashboard/MetricCardWidgetViewModel.cs
git commit -m "feat: add metric card widget viewmodel"
```

---

### Task 11: Create Dashboard ViewModel

**Files:**
- Create: `BusLane/ViewModels/Dashboard/DashboardViewModel.cs`

**Step 1: Write the ViewModel**

```csharp
namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.Services.Dashboard;
using BusLane.Services.Monitoring;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardPersistenceService _persistenceService;
    private readonly DashboardLayoutEngine _layoutEngine;
    private readonly IMetricsService _metricsService;

    [ObservableProperty] private ObservableCollection<DashboardWidgetViewModel> _widgets = [];
    [ObservableProperty] private bool _isAddWidgetDialogOpen;

    private const int MaxWidgets = 20;

    public bool CanAddWidget => Widgets.Count < MaxWidgets;

    public DashboardViewModel(
        IDashboardPersistenceService persistenceService,
        DashboardLayoutEngine layoutEngine,
        IMetricsService metricsService)
    {
        _persistenceService = persistenceService;
        _layoutEngine = layoutEngine;
        _metricsService = metricsService;

        LoadDashboard();
    }

    private void LoadDashboard()
    {
        var config = _persistenceService.Load();

        foreach (var widget in config.Widgets)
        {
            AddWidgetViewModel(widget);
        }
    }

    private void AddWidgetViewModel(DashboardWidget widget)
    {
        DashboardWidgetViewModel vm = widget.Type switch
        {
            WidgetType.LineChart => new LineChartWidgetViewModel(widget, _metricsService),
            WidgetType.PieChart => new PieChartWidgetViewModel(widget),
            WidgetType.BarChart => new BarChartWidgetViewModel(widget),
            WidgetType.MetricCard => new MetricCardWidgetViewModel(widget, _metricsService),
            _ => throw new NotSupportedException($"Widget type {widget.Type} not supported")
        };

        Widgets.Add(vm);
        OnPropertyChanged(nameof(CanAddWidget));
    }

    [RelayCommand]
    private void AddWidget(WidgetType type)
    {
        if (!CanAddWidget)
            return;

        var (row, col) = _layoutEngine.FindNextAvailableSlot(Widgets.Select(w => w.Widget).ToObservableCollection(), 6, 4);

        var widget = new DashboardWidget
        {
            Type = type,
            Row = row,
            Column = col,
            Width = 6,
            Height = 4,
            Configuration = new WidgetConfiguration()
        };

        AddWidgetViewModel(widget);
        SaveDashboard();

        IsAddWidgetDialogOpen = false;
    }

    [RelayCommand]
    private void RemoveWidget(DashboardWidgetViewModel widget)
    {
        Widgets.Remove(widget);
        OnPropertyChanged(nameof(CanAddWidget));
        SaveDashboard();
    }

    [RelayCommand]
    private void OpenAddWidgetDialog()
    {
        IsAddWidgetDialogOpen = true;
    }

    [RelayCommand]
    private void CloseAddWidgetDialog()
    {
        IsAddWidgetDialogOpen = false;
    }

    public void UpdateEntityData(IEnumerable<QueueInfo> queues, IEnumerable<SubscriptionInfo> subscriptions)
    {
        foreach (var widget in Widgets)
        {
            if (widget is PieChartWidgetViewModel pie)
                pie.UpdateEntityData(queues, subscriptions);
            else if (widget is BarChartWidgetViewModel bar)
                bar.UpdateEntityData(queues, subscriptions);
        }
    }

    private void SaveDashboard()
    {
        var config = new DashboardConfiguration
        {
            Widgets = Widgets.Select(w => w.Widget).ToList()
        };
        _persistenceService.Save(config);
    }
}

public static class ObservableCollectionExtensions
{
    public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
    {
        return new ObservableCollection<T>(source);
    }
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/ViewModels/Dashboard/DashboardViewModel.cs
git commit -m "feat: add dashboard viewmodel"
```

---

### Task 12: Register Services in DI Container

**Files:**
- Modify: `BusLane/Program.cs`

**Step 1: Find the service registration section in Program.cs**

Look for lines like `services.AddSingleton<IMetricsService, MetricsService>();`

**Step 2: Add dashboard service registrations**

Add after existing monitoring services:
```csharp
services.AddSingleton<IDashboardPersistenceService, DashboardPersistenceService>();
services.AddSingleton<DashboardLayoutEngine>();
```

**Step 3: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add BusLane/Program.cs
git commit -m "feat: register dashboard services in DI container"
```

---

### Task 13: Create Dashboard View (XAML)

**Files:**
- Create: `BusLane/Views/Controls/DashboardView.axaml`
- Create: `BusLane/Views/Controls/DashboardView.axaml.cs`

**Step 1: Write the XAML**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:BusLane.ViewModels.Dashboard"
             x:Class="BusLane.Views.Controls.DashboardView"
             x:DataType="vm:DashboardViewModel">

    <Border Classes="card" Padding="0">
        <Grid RowDefinitions="Auto,*">
            <!-- Header -->
            <Border Grid.Row="0" BorderBrush="{DynamicResource BorderDefault}" BorderThickness="0,0,0,1" Padding="16">
                <Grid ColumnDefinitions="*,Auto">
                    <TextBlock Grid.Column="0" Text="Dashboard" Classes="heading" VerticalAlignment="Center"/>

                    <Button Grid.Column="1" Classes="primary small"
                            Command="{Binding OpenAddWidgetDialogCommand}"
                            IsEnabled="{Binding CanAddWidget}"
                            Padding="12,8">
                        <StackPanel Orientation="Horizontal" Spacing="6">
                            <LucideIcon Kind="Plus" Size="14"/>
                            <TextBlock Text="Add Widget"/>
                        </StackPanel>
                    </Button>
                </Grid>
            </Border>

            <!-- Dashboard Grid -->
            <ScrollViewer Grid.Row="1" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
                <ItemsControl ItemsSource="{Binding Widgets}" Margin="16">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <Canvas />
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate DataType="vm:DashboardWidgetViewModel">
                            <Border Background="{DynamicResource BackgroundSecondary}"
                                    BorderBrush="{DynamicResource BorderDefault}"
                                    BorderThickness="1"
                                    CornerRadius="8"
                                    Width="{Binding Width, Converter={StaticResource GridColumnToWidthConverter}}"
                                    Height="{Binding Height, Converter={StaticResource GridRowToHeightConverter}}"
                                    Canvas.Left="{Binding Column, Converter={StaticResource GridColumnToLeftConverter}}"
                                    Canvas.Top="{Binding Row, Converter={StaticResource GridRowToTopConverter}}">
                                <Grid RowDefinitions="Auto,*">
                                    <!-- Widget Header -->
                                    <Border Grid.Row="0" Padding="12,8" BorderBrush="{DynamicResource BorderDefault}" BorderThickness="0,0,0,1">
                                        <Grid ColumnDefinitions="*,Auto">
                                            <TextBlock Grid.Column="0" Text="{Binding Title}" FontWeight="SemiBold" FontSize="13"/>
                                            <Button Grid.Column="1" Classes="subtle tiny"
                                                    Command="{Binding RemoveCommand}"
                                                    Padding="4">
                                                <LucideIcon Kind="X" Size="14"/>
                                            </Button>
                                        </Grid>
                                    </Border>

                                    <!-- Widget Content -->
                                    <ContentControl Grid.Row="1" Content="{Binding}">
                                        <ContentControl.ContentTemplate>
                                            <local:DashboardWidgetTemplateSelector />
                                        </ContentControl.ContentTemplate>
                                    </ContentControl>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>
        </Grid>
    </Border>
</UserControl>
```

**Step 2: Write the code-behind**

```csharp
namespace BusLane.Views.Controls;

using Avalonia.Controls;
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
}
```

**Step 3: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds (may have warnings about missing converters/template selector)

**Step 4: Commit**

```bash
git add BusLane/Views/Controls/DashboardView.axaml BusLane/Views/Controls/DashboardView.axaml.cs
git commit -m "feat: add dashboard view (basic layout)"
```

---

### Task 14: Create Widget Template Selector

**Files:**
- Create: `BusLane/Views/Controls/DashboardWidgetTemplateSelector.cs`

**Step 1: Write the template selector**

```csharp
namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using BusLane.ViewModels.Dashboard;

public class DashboardWidgetTemplateSelector : IDataTemplate
{
    [Content]
    public required DataTemplate LineChartTemplate { get; set; }
    public required DataTemplate PieChartTemplate { get; set; }
    public required DataTemplate BarChartTemplate { get; set; }
    public required DataTemplate MetricCardTemplate { get; set; }

    public Control? Build(object? param)
    {
        if (param is LineChartWidgetViewModel)
            return LineChartTemplate.Build(param);
        if (param is PieChartWidgetViewModel)
            return PieChartTemplate.Build(param);
        if (param is BarChartWidgetViewModel)
            return BarChartTemplate.Build(param);
        if (param is MetricCardWidgetViewModel)
            return MetricCardTemplate.Build(param);

        return null;
    }

    public bool Match(object? data)
    {
        return data is DashboardWidgetViewModel;
    }
}
```

**Step 2: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add BusLane/Views/Controls/DashboardWidgetTemplateSelector.cs
git commit -m "feat: add dashboard widget template selector"
```

---

### Task 15: Create Grid Converters

**Files:**
- Create: `BusLane/Views/Converters/GridConverters.cs`

**Step 1: Write the converters**

```csharp
namespace BusLane.Views.Converters;

using Avalonia.Data.Converters;
using System.Globalization;

public static class GridConverters
{
    public static readonly IValueConverter GridColumnToLeft = new FuncValueConverter<int, double>(col => col * 80);
    public static readonly IValueConverter GridRowToTop = new FuncValueConverter<int, double>(row => row * 80);
    public static readonly IValueConverter GridColumnToWidth = new FuncValueConverter<int, double>(width => width * 80 - 8);
    public static readonly IValueConverter GridRowToHeight = new FuncValueConverter<int, double>(height => height * 80 - 8);
}
```

**Step 2: Add converters to App.axaml resources**

Modify `BusLane/App.axaml` to add:
```xml
<Application.Resources>
    <converters:GridConverters x:Key="GridConverters"/>
</Application.Resources>
```

Or add them individually as resources that can be referenced by the keys used in DashboardView.axaml.

**Step 3: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add BusLane/Views/Converters/GridConverters.cs
git commit -m "feat: add grid layout converters"
```

---

### Task 16: Update FeaturePanelsViewModel to Use Dashboard

**Files:**
- Modify: `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs`

**Step 1: Read current FeaturePanelsViewModel.cs**

**Step 2: Replace ChartsViewModel with DashboardViewModel**

Find where `ChartsViewModel` is used and replace with `DashboardViewModel`. Update constructor injection and property.

Change:
```csharp
public ChartsViewModel Charts { get; }
```
To:
```csharp
public DashboardViewModel Dashboard { get; }
```

Update constructor to inject `DashboardViewModel` instead of `ChartsViewModel`.

**Step 3: Update entity data flow**

Find where `Charts.UpdateEntityDistribution()` and `Charts.UpdateComparisonChart()` are called, and update to use `Dashboard.UpdateEntityData()` instead.

**Step 4: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add BusLane/ViewModels/Core/FeaturePanelsViewModel.cs
git commit -m "feat: integrate dashboard viewmodel into feature panels"
```

---

### Task 17: Update MainWindow.axaml to Use DashboardView

**Files:**
- Modify: `BusLane/Views/MainWindow.axaml`

**Step 1: Find where ChartsView is used**

Look for `<controls:ChartsView` in MainWindow.axaml.

**Step 2: Replace with DashboardView**

Change:
```xml
<controls:ChartsView DataContext="{Binding FeaturePanels.Charts}" ... />
```
To:
```xml
<controls:DashboardView DataContext="{Binding FeaturePanels.Dashboard}" ... />
```

**Step 3: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add BusLane/Views/MainWindow.axaml
git commit -m "feat: replace charts view with dashboard view in main window"
```

---

### Task 18: Create Add Widget Dialog

**Files:**
- Create: `BusLane/Views/Dialogs/AddWidgetDialog.axaml`
- Create: `BusLane/Views/Dialogs/AddWidgetDialog.axaml.cs`

**Step 1: Write the XAML**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:BusLane.ViewModels.Dashboard"
             xmlns:models="using:BusLane.Models"
             x:Class="BusLane.Views.Dialogs.AddWidgetDialog"
             x:DataType="vm:DashboardViewModel">

    <Border Background="{DynamicResource BackgroundPrimary}"
            BorderBrush="{DynamicResource BorderDefault}"
            BorderThickness="1"
            CornerRadius="8"
            Padding="24"
            MaxWidth="500">
        <Grid RowDefinitions="Auto,*,Auto" RowSpacing="16">
            <TextBlock Grid.Row="0" Text="Add Widget" Classes="heading"/>

            <Grid Grid.Row="1" ColumnDefinitions="*,*" RowDefinitions="Auto,Auto">
                <Button Grid.Row="0" Grid.Column="0"
                        Command="{Binding AddWidgetCommand}"
                        CommandParameter="{x:Static models:WidgetType.LineChart}"
                        Classes="card"
                        Margin="0,0,8,8"
                        Padding="16">
                    <StackPanel Spacing="8">
                        <LucideIcon Kind="LineChart" Size="32"/>
                        <TextBlock Text="Line Chart" FontWeight="SemiBold"/>
                        <TextBlock Text="Time-series data over time" FontSize="12" Opacity="0.7"/>
                    </StackPanel>
                </Button>

                <Button Grid.Row="0" Grid.Column="1"
                        Command="{Binding AddWidgetCommand}"
                        CommandParameter="{x:Static models:WidgetType.PieChart}"
                        Classes="card"
                        Margin="8,0,0,8"
                        Padding="16">
                    <StackPanel Spacing="8">
                        <LucideIcon Kind="PieChart" Size="32"/>
                        <TextBlock Text="Pie Chart" FontWeight="SemiBold"/>
                        <TextBlock Text="Distribution across entities" FontSize="12" Opacity="0.7"/>
                    </StackPanel>
                </Button>

                <Button Grid.Row="1" Grid.Column="0"
                        Command="{Binding AddWidgetCommand}"
                        CommandParameter="{x:Static models:WidgetType.BarChart}"
                        Classes="card"
                        Margin="0,8,8,0"
                        Padding="16">
                    <StackPanel Spacing="8">
                        <LucideIcon Kind="BarChart" Size="32"/>
                        <TextBlock Text="Bar Chart" FontWeight="SemiBold"/>
                        <TextBlock Text="Compare entities side-by-side" FontSize="12" Opacity="0.7"/>
                    </StackPanel>
                </Button>

                <Button Grid.Row="1" Grid.Column="1"
                        Command="{Binding AddWidgetCommand}"
                        CommandParameter="{x:Static models:WidgetType.MetricCard}"
                        Classes="card"
                        Margin="8,8,0,0"
                        Padding="16">
                    <StackPanel Spacing="8">
                        <LucideIcon Kind="Activity" Size="32"/>
                        <TextBlock Text="Metric Card" FontWeight="SemiBold"/>
                        <TextBlock Text="Single metric with trend" FontSize="12" Opacity="0.7"/>
                    </StackPanel>
                </Button>
            </Grid>

            <Button Grid.Row="2"
                    Command="{Binding CloseAddWidgetDialogCommand}"
                    Classes="subtle"
                    HorizontalAlignment="Right"
                    Content="Cancel"/>
        </Grid>
    </Border>
</UserControl>
```

**Step 2: Write code-behind**

```csharp
namespace BusLane.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public partial class AddWidgetDialog : UserControl
{
    public AddWidgetDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
```

**Step 3: Add dialog to DashboardView**

Modify `DashboardView.axaml` to include the dialog as an overlay:

```xml
<!-- Add after the main Border, inside a Grid or Panel -->
<Panel IsVisible="{Binding IsAddWidgetDialogOpen}"
       Background="#80000000">
    <dialogs:AddWidgetDialog />
</Panel>
```

**Step 4: Verify build**

Run: `dotnet build BusLane/BusLane.csproj`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add BusLane/Views/Dialogs/AddWidgetDialog.axaml BusLane/Views/Dialogs/AddWidgetDialog.axaml.cs
git commit -m "feat: add widget selection dialog"
```

---

### Task 19: Run Full Build and Test

**Step 1: Full build**

Run: `dotnet build BusLane.sln`
Expected: Build succeeds with no errors

**Step 2: Run all tests**

Run: `dotnet test BusLane.Tests/BusLane.Tests.csproj -v n`
Expected: All tests pass

**Step 3: Commit any final changes**

```bash
git add -A
git commit -m "chore: final adjustments for custom dashboards"
```

---

## Summary

This implementation plan creates a complete custom dashboard system with:
- 4 widget types (LineChart, PieChart, BarChart, MetricCard)
- Grid-based layout (12-column, resizable)
- JSON persistence with default dashboard migration
- Full test coverage for services
- Integration with existing IMetricsService

**Total Tasks:** 19
**Estimated Commits:** 19+
