# Custom Dashboards Design Document

**Date:** 2026-01-30
**Status:** Approved

## Overview

Replace the static charts view with a dynamic, customizable dashboard system. Users can add multiple widget instances, arrange them in a responsive grid, resize them, and persist their layouts. Widgets include line charts, pie charts, bar charts, and metric cards, all fed by the existing `IMetricsService`.

## Requirements

- Grid-based layout (12-column system) with draggable, resizable widgets
- Widget types: LineChart, PieChart, BarChart, MetricCard
- Each widget independently configurable (metric type, entity filter, time range)
- Add/remove widgets via UI dialogs
- Auto-save dashboard configuration to JSON
- Default dashboard matches current 4-chart layout for migration
- Maximum 20 widgets per dashboard
- Graceful error handling for individual widgets

## Data Models

```csharp
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
    public int Row { get; init; }
    public int Column { get; init; }
    public int Width { get; init; }  // 3-12 columns
    public int Height { get; init; } // 2+ rows
    public WidgetConfiguration Configuration { get; init; } = new();
}

public record WidgetConfiguration
{
    public string? Title { get; init; }
    public string MetricName { get; init; } = "ActiveMessageCount";
    public string? EntityFilter { get; init; } // null = aggregated
    public string TimeRange { get; init; } = "1 Hour";
    public int TopEntities { get; init; } = 10; // for pie/bar charts
    public bool ShowSecondaryMetric { get; init; } = false; // for bar charts
}
```

## Architecture

### Services

**DashboardPersistenceService**
```csharp
public interface IDashboardPersistenceService
{
    DashboardConfiguration Load();
    void Save(DashboardConfiguration config);
    DashboardConfiguration GetDefaultConfiguration();
}
```

**DashboardLayoutEngine**
```csharp
public interface IDashboardLayoutEngine
{
    (int row, int col) FindNextAvailableSlot(int width, int height);
    bool CanResize(DashboardWidget widget, int newWidth, int newHeight);
    bool CanMove(DashboardWidget widget, int newRow, int newCol);
    void CompactRows();
}
```

### ViewModels

**DashboardViewModel**
- `ObservableCollection<DashboardWidgetViewModel> Widgets`
- `AddWidgetCommand`, `RemoveWidgetCommand`
- `SaveDashboardCommand` (auto-saves on changes)

**DashboardWidgetViewModel** (abstract base)
- `DashboardWidget Configuration` (model)
- `int Row, Column, Width, Height`
- `string Title`
- `ICommand ConfigureCommand`, `RemoveCommand`
- Abstract `RefreshData()` method

**LineChartWidgetViewModel** : DashboardWidgetViewModel
- `ObservableCollection<ISeries> Series`
- `Axis[] XAxes, YAxes`
- Implements time-series data binding from `IMetricsService`

**PieChartWidgetViewModel** : DashboardWidgetViewModel
- `ObservableCollection<ISeries> Series`
- Distribution data from current entity state

**BarChartWidgetViewModel** : DashboardWidgetViewModel
- `ObservableCollection<ISeries> Series`
- `Axis[] XAxes, YAxes`
- Comparison data across entities

**MetricCardWidgetViewModel** : DashboardWidgetViewModel
- `double CurrentValue`
- `double PreviousValue` (for trend)
- `bool IsTrendUp`
- `string TrendPercentage`

## UI Components

### DashboardView (UserControl)

Replaces `ChartsView.axaml`. Uses `ItemsControl` with custom `Grid` panel:

```xml
<ItemsControl ItemsSource="{Binding Widgets}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <Grid x:Name="DashboardGrid" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```

### DashboardWidgetContainer (UserControl)

Wrapper for all widgets providing:
- Header with title and action buttons (settings, remove)
- Content area hosting the actual widget
- Resize handle (bottom-right)
- Drag handle (header)
- Error state overlay

### AddWidgetDialog

Modal dialog showing available widget types as selectable tiles. Creates widget with default configuration and opens configuration dialog immediately.

### WidgetConfigurationDialog

Context-aware configuration dialog:
- **All widgets**: Title input, metric dropdown
- **Line charts**: Entity filter (dropdown with "Aggregated" option), time range dropdown
- **Pie/Bar charts**: Top N entities spinner, metric dropdown
- **Bar charts**: Show secondary metric checkbox
- **Metric cards**: Comparison period dropdown (Previous Hour, Previous Day)

## Integration Points

**DI Registration** (Program.cs):
```csharp
services.AddSingleton<IDashboardPersistenceService, DashboardPersistenceService>();
services.AddSingleton<DashboardLayoutEngine>();
```

**FeaturePanelsViewModel**:
Replace `ChartsViewModel` with `DashboardViewModel`. Initialize with loaded or default configuration.

**MetricsService**:
Existing `IMetricsService` feeds all widgets. Widgets subscribe to `MetricRecorded` event for real-time updates.

**Migration**:
On first run with no dashboard config, create default with 4 widgets matching current layout:
1. Line chart: Active messages over time (aggregated)
2. Line chart: Dead letters over time (aggregated)
3. Pie chart: Message distribution (top 10)
4. Bar chart: Entity comparison (top 10)

## Error Handling

**Widget Errors**: Each widget catches exceptions in `RefreshData()`, shows inline error state with retry button. Other widgets unaffected.

**Grid Conflicts**: Layout engine attempts auto-push of conflicting widgets. If impossible, operation rejected with visual feedback (red outline).

**Persistence Errors**: Corrupted config backed up and replaced with default. Log error for debugging.

**Performance Guardrails**:
- Max 20 widgets enforced at UI level
- Charts sample to 100 points max
- Widgets throttle updates (100ms batching for high-frequency changes)

## Testing Strategy

### Unit Tests

- `DashboardLayoutEngineTests`: Grid operations, collision detection, compaction
- `DashboardPersistenceServiceTests`: Save/load, migration, corruption handling
- `WidgetViewModelTests`: Configuration binding, data refresh, error states

### Integration Tests

- `DashboardViewModelTests`: Full widget lifecycle, persistence integration
- Verify metrics flow from service to chart rendering
- Verify add/remove operations update UI and persistence

### Manual Testing

- Add each widget type, verify configuration
- Drag/resize widgets, verify grid behavior
- Restart app, verify dashboard persistence
- Test widget limit enforcement
- Disconnect Service Bus, verify error states
- Test migration from old version

## Files to Create/Modify

### New Files
- `BusLane/Services/Dashboard/IDashboardPersistenceService.cs`
- `BusLane/Services/Dashboard/DashboardPersistenceService.cs`
- `BusLane/Services/Dashboard/DashboardLayoutEngine.cs`
- `BusLane/Models/DashboardConfiguration.cs`
- `BusLane/ViewModels/Dashboard/DashboardViewModel.cs`
- `BusLane/ViewModels/Dashboard/DashboardWidgetViewModel.cs`
- `BusLane/ViewModels/Dashboard/LineChartWidgetViewModel.cs`
- `BusLane/ViewModels/Dashboard/PieChartWidgetViewModel.cs`
- `BusLane/ViewModels/Dashboard/BarChartWidgetViewModel.cs`
- `BusLane/ViewModels/Dashboard/MetricCardWidgetViewModel.cs`
- `BusLane/Views/Controls/DashboardView.axaml`
- `BusLane/Views/Controls/DashboardView.axaml.cs`
- `BusLane/Views/Controls/DashboardWidgetContainer.axaml`
- `BusLane/Views/Controls/DashboardWidgetContainer.axaml.cs`
- `BusLane/Views/Dialogs/AddWidgetDialog.axaml`
- `BusLane/Views/Dialogs/WidgetConfigurationDialog.axaml`

### Modified Files
- `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs` (replace ChartsViewModel)
- `BusLane/Program.cs` (register new services)
- `BusLane/Models/AppPaths.cs` (add DashboardConfig path)

## Future Considerations

- Multiple named dashboards (tabs)
- Widget templates (save/restore widget configurations)
- Additional widget types (alert summary, live stream preview)
- Import/export dashboard configurations
- Time range sync across widgets (optional global override)
