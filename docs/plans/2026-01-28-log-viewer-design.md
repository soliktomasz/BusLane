# Log Viewer Design Document

**Date:** 2026-01-28
**Status:** Approved

## Overview

A right-side slide-out panel containing a combined log viewer with color-coded entries. The panel is collapsed by default and opens via a toolbar button. Logs are captured from both Application and Service Bus operations sources, each tagged with severity levels (Info, Warning, Error, Debug). Debug logs only appear when debug mode is enabled.

## Requirements

- Combined view of Application and Service Bus operation logs
- Color coding by severity (Info, Warning, Error, Debug)
- Debug mode to show/hide Debug-level logs
- Current session only (no persistence)
- Actions: View, Copy, Clear, Filter, Auto-scroll
- Default state: Collapsed
- Toggle button in main toolbar

## Data Models

```csharp
public enum LogSource
{
    Application,
    ServiceBus
}

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Debug
}

public record LogEntry(
    DateTime Timestamp,
    LogSource Source,
    LogLevel Level,
    string Message,
    string? Details = null
);
```

## Architecture

### ILogSink Interface

```csharp
public interface ILogSink
{
    void Log(LogEntry entry);
    IReadOnlyList<LogEntry> GetLogs();
    void Clear();
    event Action<LogEntry>? OnLogAdded;
}
```

### Implementation

`LogSink` - singleton implementation with:
- Bounded circular buffer (1000 entries)
- Thread-safe operations
- Reactive event for UI updates

### Logging Integration

1. **Microsoft.Extensions.Logging adapter** - wraps `ILogSink` for general app logging
2. **Direct logging** - Service Bus operations call `ILogSink.Log()` directly for structured entries

## UI Components

### LogViewerPanel

Right-side slide-out UserControl with:

**Header:**
- Title: "Activity Log"
- Close button (X)
- Auto-scroll toggle
- Clear logs button
- Copy all button

**Filter Bar:**
- Severity dropdown (All, Info, Warning, Error, Debug)
- Source dropdown (All, Application, Service Bus)
- Search text box
- Count indicator

**Log List:**
- Virtualized list
- Color coding:
  - Info: Blue
  - Warning: Orange
  - Error: Red
  - Debug: Purple
- Right-click: Copy entry, Copy message
- Double-click: Expand multi-line messages
- Auto-scroll when enabled

### LogEntryControl

DataTemplate for individual log items with color coding.

### LogViewerViewModel

- `IReadOnlyList<LogEntry> FilteredLogs`
- `LogLevel SelectedLevelFilter`
- `LogSource SelectedSourceFilter`
- `string SearchText`
- `bool IsAutoScrollEnabled`
- `bool IsDebugModeEnabled`
- Commands: `ClearLogsCommand`, `CopyAllCommand`, `ToggleAutoScrollCommand`

## Integration Points

- Registered in DI container as singleton
- Toggled via `MainWindowViewModel.ShowLogViewer` boolean
- Log entries emitted from:
  - `ConnectionViewModel` - connection events
  - `MessageOperationsViewModel` - message operations
  - `FeaturePanelsViewModel` - monitoring events
  - Background services

## Error Handling

- Log overflow: Circular buffer removes oldest entries
- Filter mismatch: Empty state with "Clear filters" button
- Clipboard failures: Status message notification
- High throughput: Throttled UI updates (100ms batching)

## Testing Strategy

### Unit Tests

- `LogSinkTests` - Add, Get, Clear, overflow, events
- `LogViewerViewModelTests` - Filter logic, search, auto-scroll

### Integration Tests

- Logging from services appears in sink
- Debug mode toggle filtering
- Panel open/close state

### Manual Testing

- Panel animation smoothness
- Log coloring accuracy
- Copy/Clear/Filter functionality
