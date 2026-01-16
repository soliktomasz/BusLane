# Multi-Connection Tabs Design

## Overview

Add support for multiple simultaneous Service Bus connections via a tabbed interface. Users can open multiple namespaces/connections in separate tabs, enabling message flow tracking across environments and general monitoring of distributed systems.

## Goals

- Allow users to connect to multiple Service Bus namespaces simultaneously
- Provide a familiar tabbed interface for managing connections
- Keep the UI clean and uncluttered despite added complexity
- Enable cross-connection monitoring through global features
- Maintain backward compatibility with current single-connection workflows

## Non-Goals

- Pop-out/detachable windows (future enhancement)
- Per-tab settings or preferences
- Cross-tab message transfer operations

## Use Cases

1. **Message flow tracking**: Follow a message as it moves between namespaces (e.g., ingress → processing → output)
2. **Environment comparison**: View the same queue across Dev/Staging/Production
3. **General monitoring**: Keep multiple Service Bus resources visible for operational awareness

## Architecture

### High-Level Structure

The `MainWindowViewModel` becomes a shell coordinator managing tabs and global features:

```
MainWindowViewModel (Shell)
├── ConnectionTabs: ObservableCollection<ConnectionTabViewModel>
├── ActiveTab: ConnectionTabViewModel
├── FeaturePanels: FeaturePanelsViewModel (global)
├── Settings, Alerts, etc. (global)
│
└── ConnectionTabViewModel (per tab)
    ├── Navigation: NavigationState
    ├── MessageOps: MessageOperationsViewModel
    ├── Operations: IServiceBusOperations
    ├── TabTitle: string
    ├── TabSubtitle: string
    └── ConnectionInfo: SavedConnection | ServiceBusNamespace
```

### ConnectionTabViewModel

Core component encapsulating a single connection's state:

```csharp
public partial class ConnectionTabViewModel : ViewModelBase
{
    // Identity
    [ObservableProperty] private string _tabId;           // Unique GUID
    [ObservableProperty] private string _tabTitle;        // "Production"
    [ObservableProperty] private string _tabSubtitle;     // "orders-prod.servicebus.windows.net"
    [ObservableProperty] private ConnectionMode _mode;    // Azure or ConnectionString

    // State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string? _statusMessage;

    // Composed components
    public NavigationState Navigation { get; }
    public MessageOperationsViewModel MessageOps { get; }

    // Connection resources
    private IServiceBusOperations? _operations;
    private SavedConnection? _savedConnection;
    private ServiceBusNamespace? _namespace;

    // Lifecycle
    public async Task ConnectAsync(SavedConnection connection, IServiceBusOperationsFactory factory);
    public async Task ConnectAsync(ServiceBusNamespace ns, TokenCredential credential, IServiceBusOperationsFactory factory);
    public async Task DisconnectAsync();
    public void Dispose();
}
```

### TabSessionState

Model for persisting open tabs across app restarts:

```csharp
public record TabSessionState
{
    public string TabId { get; init; }
    public ConnectionMode Mode { get; init; }
    public string? ConnectionId { get; init; }      // Reference to SavedConnection
    public string? NamespaceId { get; init; }       // For Azure mode
    public string? SelectedEntityName { get; init; }
    public int TabOrder { get; init; }
}
```

### MainWindowViewModel Changes

New properties and methods:

```csharp
public partial class MainWindowViewModel : ViewModelBase
{
    // Tab management
    public ObservableCollection<ConnectionTabViewModel> ConnectionTabs { get; } = new();
    [ObservableProperty] private ConnectionTabViewModel? _activeTab;

    // Tab operations
    public async Task OpenTabForConnection(SavedConnection connection);
    public async Task OpenTabForNamespace(ServiceBusNamespace ns);
    public void CloseTab(string tabId);
    public void SwitchToTab(string tabId);
    public void ReorderTabs(int oldIndex, int newIndex);

    // Session management
    public async Task SaveSessionAsync();
    public async Task RestoreSessionAsync();

    // Delegated status (from active tab)
    public string? StatusMessage => ActiveTab?.StatusMessage;
}
```

## Tab Lifecycle

### Opening Tabs

Three entry points converging on shared logic:

1. **Connection Library**: Select saved connection → `OpenTabForConnection()`
2. **New Tab Button (+)**: Click → picker popup → select connection → `OpenTabForConnection()`
3. **Drag-and-Drop**: Drag from favorites sidebar onto tab bar → `OpenTabForConnection()`

```csharp
public async Task OpenTabForConnection(SavedConnection connection)
{
    var tab = new ConnectionTabViewModel(
        tabId: Guid.NewGuid().ToString(),
        title: connection.Name,
        subtitle: connection.Endpoint,
        mode: ConnectionMode.ConnectionString
    );

    ConnectionTabs.Add(tab);
    ActiveTab = tab;

    await tab.ConnectAsync(connection, _operationsFactory);
}
```

### Closing Tabs

- Close button on tab triggers `CloseTab(tabId)`
- Dispose `IServiceBusOperations` and clear collections
- Switch to nearest tab, or show empty state if last tab closed

### Session Persistence

Controlled by user preference `RestoreTabsOnStartup`:

**On exit** (if enabled):
- Serialize `ConnectionTabs` to `List<TabSessionState>`
- Save to preferences storage

**On launch** (if enabled):
- Load saved session
- Recreate tabs with loading indicators
- Reconnect each tab
- Show "Reconnect" button on failures

## UI Design

### Layout

```
┌─────────────────────────────────────────────────────────────────┐
│  [Azure Sign In]  [Connection Library]  [Settings]  [?]         │  Toolbar
├─────────────────────────────────────────────────────────────────┤
│  [Production ▼] [Staging ▼] [Dev ▼]                        [+]  │  Tab Bar
│   orders-prod    orders-stg   orders-dev                        │
├──────────────────┬──────────────────────────────────────────────┤
│  Queues          │  Messages                                    │
│  ├─ orders       │  ┌────────────────────────────────────────┐  │
│  ├─ payments     │  │ Message list for active tab            │  │
│  Topics          │  │                                        │  │
│  ├─ events       │  └────────────────────────────────────────┘  │
│                  │  [Message detail panel]                      │
├──────────────────┴──────────────────────────────────────────────┤
│  Status: Connected to orders-prod.servicebus.windows.net        │  Footer
└─────────────────────────────────────────────────────────────────┘
```

### Tab Visual Design

Each tab displays:
- **Primary text**: Connection name (bold, truncated if needed)
- **Secondary text**: Namespace endpoint (smaller, muted)
- **Close button (×)**: Visible on hover
- **Status indicator**: Colored dot (green=connected, yellow=connecting, red=error)

### Tab Interactions

| Action | Behavior |
|--------|----------|
| Click tab | Switch to that connection |
| Middle-click tab | Close tab |
| Right-click tab | Context menu: Close, Close Others, Close All, Duplicate |
| Drag tab | Reorder tabs |
| Click [+] | Show connection picker dropdown |
| Drop connection on bar | Open in new tab |

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+T | New tab (show picker) |
| Ctrl+W | Close current tab |
| Ctrl+Tab | Next tab |
| Ctrl+Shift+Tab | Previous tab |
| Ctrl+1-9 | Switch to tab by position |

### Empty State

When no tabs are open, show centered prompt:
- "Open a connection to get started"
- Quick action buttons: "Sign in with Azure" / "Open Connection Library"

## Global Features Integration

### FeaturePanelsViewModel Updates

Gains access to all tabs for aggregation:

```csharp
public class FeaturePanelsViewModel
{
    private readonly Func<IEnumerable<ConnectionTabViewModel>> _getAllTabs;
    private readonly Func<ConnectionTabViewModel?> _getActiveTab;

    public IEnumerable<QueueInfo> AllQueues =>
        _getAllTabs().SelectMany(t => t.Navigation.Queues);

    public IEnumerable<SubscriptionInfo> AllSubscriptions =>
        _getAllTabs().SelectMany(t => t.Navigation.TopicSubscriptions);
}
```

### Live Stream

Operates on active tab's selected entity. Automatically switches source when:
- User switches tabs
- User selects different entity in current tab

### Alerts

Enhanced to support cross-connection monitoring:

```csharp
public class AlertRule
{
    public string? TabId { get; set; }         // null = match any tab
    public string EntityPattern { get; set; }  // e.g., "orders-*"
    public AlertCondition Condition { get; set; }
}
```

Alert notifications include connection name for context.

### Charts/Metrics

Metrics keyed by endpoint + entity name. Supports:
- Per-connection entity charts (current behavior)
- Cross-connection comparison (e.g., same queue across environments)

## Data Flow

### Command Delegation

View binds directly to `ActiveTab` sub-properties:

```xml
<views:NavigationPanel DataContext="{Binding ActiveTab.Navigation}" />
<views:MessageListPanel DataContext="{Binding ActiveTab.MessageOps}" />
```

Shell handles tab-level commands:

```csharp
[RelayCommand]
private void CloseCurrentTab()
{
    if (ActiveTab != null)
        CloseTab(ActiveTab.TabId);
}
```

### Tab Switching

```
User clicks tab
    → Shell.SwitchToTab(tabId)
    → ActiveTab = ConnectionTabs.First(t => t.TabId == tabId)
    → OnPropertyChanged(nameof(ActiveTab))
    → UI rebinds to new tab's Navigation and MessageOps
    → FeaturePanels updates live stream source if running
```

### Status Messages

Shell exposes active tab's status:

```csharp
public string? StatusMessage => ActiveTab?.StatusMessage;

partial void OnActiveTabChanged(ConnectionTabViewModel? value)
{
    // Unsubscribe from old tab
    // Subscribe to new tab's PropertyChanged for StatusMessage
    OnPropertyChanged(nameof(StatusMessage));
}
```

## Memory Management

### Resource Cleanup on Tab Close

```csharp
public void Dispose()
{
    _operations?.DisposeAsync().AsTask().Wait();
    Navigation.Clear();
    MessageOps.Clear();
}
```

### Lazy Loading Consideration

For tabs inactive for extended periods, consider:
- Releasing message cache
- Reloading on tab activation
- Showing stale indicator until refresh completes

## Implementation Phases

### Phase 1: Core Tab Infrastructure
- Create `ConnectionTabViewModel`
- Refactor `MainWindowViewModel` to use `ObservableCollection<ConnectionTabViewModel>`
- Migrate single-connection logic into tab
- **Result**: App works as before, using one internal tab

### Phase 2: Tab Bar UI
- Create `TabBarView` user control
- Implement click-to-switch and close button
- Add [+] button with connection picker
- Style tabs with name, subtitle, status indicator
- **Result**: Visual tabs functional

### Phase 3: Multi-Connection Support
- Enable multiple simultaneous tabs
- Independent `IServiceBusOperations` per tab
- Handle Azure mode (shared credential, separate operations)
- Update command routing through `ActiveTab`
- **Result**: Full multi-tab working

### Phase 4: Session Persistence
- Create `TabSessionState` model
- Add `RestoreTabsOnStartup` preference
- Serialize/deserialize tabs on exit/launch
- Handle reconnection failures gracefully
- **Result**: Tabs persist across restarts

### Phase 5: Polish and UX
- Drag-to-reorder tabs
- Drag-from-sidebar to open tab
- Tab context menu
- Keyboard shortcuts
- Empty state UI
- **Result**: Production-ready experience

### Phase 6: Global Features Update
- Cross-tab alert rules
- Cross-connection metrics comparison
- **Result**: Monitoring leverages multi-connection

## File Changes Summary

### New Files
- `ViewModels/Core/ConnectionTabViewModel.cs`
- `ViewModels/Core/TabSessionState.cs`
- `Views/Controls/TabBarView.axaml` + `.axaml.cs`
- `Views/Controls/ConnectionPickerPopup.axaml` + `.axaml.cs`

### Modified Files
- `ViewModels/MainWindowViewModel.cs` - Refactor to shell pattern
- `ViewModels/Core/FeaturePanelsViewModel.cs` - Multi-tab access
- `Views/MainWindow.axaml` - Add tab bar, rebind to ActiveTab
- `Services/Infrastructure/PreferencesService.cs` - Add session restore preference
- `Models/UserPreferences.cs` - Add RestoreTabsOnStartup

### Minimal Changes
- `NavigationState.cs` - No changes (used as-is per tab)
- `MessageOperationsViewModel.cs` - No changes (used as-is per tab)

## Testing Strategy

### Unit Tests
- `ConnectionTabViewModel` lifecycle (connect, disconnect, dispose)
- Tab collection management (add, remove, reorder)
- Session serialization/deserialization
- Active tab switching and property propagation

### Integration Tests
- Multiple tabs with real Service Bus connections
- Alert evaluation across multiple tabs
- Session restore with mixed connection types

### Manual Testing
- Tab interactions (click, middle-click, right-click, drag)
- Keyboard shortcuts
- Edge cases: close last tab, rapid tab switching, connection failures
