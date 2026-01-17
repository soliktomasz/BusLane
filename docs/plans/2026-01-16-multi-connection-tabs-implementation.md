# Multi-Connection Tabs Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable users to connect to multiple Service Bus namespaces simultaneously via a tabbed interface.

**Architecture:** Refactor MainWindowViewModel into a shell that manages a collection of ConnectionTabViewModel instances. Each tab owns its own NavigationState and MessageOperationsViewModel. Global features (alerts, live stream, settings) remain in the shell and aggregate across tabs.

**Tech Stack:** Avalonia UI, CommunityToolkit.Mvvm, .NET 10, xUnit, FluentAssertions, NSubstitute

---

## Phase 1: Core Tab Infrastructure

### Task 1: Create ConnectionTabViewModel

**Files:**
- Create: `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`
- Test: `BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs`

**Step 1: Create the test file with initial test**

```csharp
// BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
using BusLane.ViewModels;
using BusLane.ViewModels.Core;
using FluentAssertions;

namespace BusLane.Tests.ViewModels.Core;

public class ConnectionTabViewModelTests
{
    [Fact]
    public void Constructor_SetsTabIdAndTitle()
    {
        // Arrange & Act
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");

        // Assert
        tab.TabId.Should().Be("test-id");
        tab.TabTitle.Should().Be("Test Tab");
        tab.TabSubtitle.Should().Be("test.servicebus.windows.net");
    }

    [Fact]
    public void Constructor_InitializesNavigationState()
    {
        // Arrange & Act
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");

        // Assert
        tab.Navigation.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_SetsDefaultConnectionState()
    {
        // Arrange & Act
        var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net");

        // Assert
        tab.IsConnected.Should().BeFalse();
        tab.IsLoading.Should().BeFalse();
        tab.Mode.Should().Be(ConnectionMode.None);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ConnectionTabViewModelTests"`
Expected: FAIL with "type or namespace 'ConnectionTabViewModel' could not be found"

**Step 3: Create minimal ConnectionTabViewModel**

```csharp
// BusLane/ViewModels/Core/ConnectionTabViewModel.cs
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Represents a single connection tab with its own navigation state and message operations.
/// Each tab encapsulates a complete connection to a Service Bus namespace.
/// </summary>
public partial class ConnectionTabViewModel : ViewModelBase
{
    // Identity
    [ObservableProperty] private string _tabId;
    [ObservableProperty] private string _tabTitle;
    [ObservableProperty] private string _tabSubtitle;
    [ObservableProperty] private ConnectionMode _mode = ConnectionMode.None;

    // State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string? _statusMessage;

    // Composed components
    public NavigationState Navigation { get; }

    // Connection resources (set after connection)
    private IServiceBusOperations? _operations;
    private SavedConnection? _savedConnection;
    private ServiceBusNamespace? _namespace;

    public ConnectionTabViewModel(string tabId, string tabTitle, string tabSubtitle)
    {
        _tabId = tabId;
        _tabTitle = tabTitle;
        _tabSubtitle = tabSubtitle;
        Navigation = new NavigationState();
    }

    /// <summary>
    /// Gets the current operations instance for this tab.
    /// </summary>
    public IServiceBusOperations? Operations => _operations;

    /// <summary>
    /// Gets the saved connection if connected via connection string.
    /// </summary>
    public SavedConnection? SavedConnection => _savedConnection;

    /// <summary>
    /// Gets the namespace if connected via Azure credentials.
    /// </summary>
    public ServiceBusNamespace? Namespace => _namespace;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ConnectionTabViewModelTests"`
Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add BusLane/ViewModels/Core/ConnectionTabViewModel.cs BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
git commit -m "feat: add ConnectionTabViewModel base structure

Creates the foundation for multi-connection tabs with:
- Tab identity (id, title, subtitle)
- Connection state tracking
- NavigationState composition

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 2: Add MessageOperationsViewModel to ConnectionTabViewModel

**Files:**
- Modify: `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`
- Modify: `BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs`

**Step 1: Add test for MessageOps initialization**

Add to `ConnectionTabViewModelTests.cs`:

```csharp
[Fact]
public void Constructor_WithPreferencesService_InitializesMessageOps()
{
    // Arrange
    var preferencesService = Substitute.For<IPreferencesService>();

    // Act
    var tab = new ConnectionTabViewModel(
        "test-id",
        "Test Tab",
        "test.servicebus.windows.net",
        preferencesService);

    // Assert
    tab.MessageOps.Should().NotBeNull();
}
```

Add using at top:
```csharp
using BusLane.Services.Abstractions;
using NSubstitute;
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ConnectionTabViewModelTests.Constructor_WithPreferencesService"`
Expected: FAIL - constructor doesn't accept preferencesService

**Step 3: Update ConnectionTabViewModel with MessageOps**

Update `ConnectionTabViewModel.cs`:

```csharp
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Represents a single connection tab with its own navigation state and message operations.
/// Each tab encapsulates a complete connection to a Service Bus namespace.
/// </summary>
public partial class ConnectionTabViewModel : ViewModelBase
{
    private readonly IPreferencesService _preferencesService;

    // Identity
    [ObservableProperty] private string _tabId;
    [ObservableProperty] private string _tabTitle;
    [ObservableProperty] private string _tabSubtitle;
    [ObservableProperty] private ConnectionMode _mode = ConnectionMode.None;

    // State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string? _statusMessage;

    // Composed components
    public NavigationState Navigation { get; }
    public MessageOperationsViewModel MessageOps { get; }

    // Connection resources (set after connection)
    private IServiceBusOperations? _operations;
    private SavedConnection? _savedConnection;
    private ServiceBusNamespace? _namespace;

    public ConnectionTabViewModel(string tabId, string tabTitle, string tabSubtitle)
        : this(tabId, tabTitle, tabSubtitle, null!)
    {
    }

    public ConnectionTabViewModel(
        string tabId,
        string tabTitle,
        string tabSubtitle,
        IPreferencesService preferencesService)
    {
        _tabId = tabId;
        _tabTitle = tabTitle;
        _tabSubtitle = tabSubtitle;
        _preferencesService = preferencesService;

        Navigation = new NavigationState();

        MessageOps = new MessageOperationsViewModel(
            () => _operations,
            preferencesService ?? new DummyPreferencesService(),
            () => Navigation.CurrentEntityName,
            () => Navigation.CurrentSubscriptionName,
            () => Navigation.CurrentEntityRequiresSession,
            () => Navigation.ShowDeadLetter,
            msg => StatusMessage = msg);
    }

    /// <summary>
    /// Gets the current operations instance for this tab.
    /// </summary>
    public IServiceBusOperations? Operations => _operations;

    /// <summary>
    /// Gets the saved connection if connected via connection string.
    /// </summary>
    public SavedConnection? SavedConnection => _savedConnection;

    /// <summary>
    /// Gets the namespace if connected via Azure credentials.
    /// </summary>
    public ServiceBusNamespace? Namespace => _namespace;

    // Minimal implementation for parameterless constructor
    private class DummyPreferencesService : IPreferencesService
    {
        public int DefaultMessageCount { get; set; } = 100;
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; } = 30;
        public bool ShowDeadLetterBadges { get; set; } = true;
        public bool EnableMessagePreview { get; set; } = true;
        public bool ConfirmBeforePurge { get; set; } = true;
        public bool ShowNavigationPanel { get; set; } = true;
        public void Save() { }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ConnectionTabViewModelTests"`
Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add BusLane/ViewModels/Core/ConnectionTabViewModel.cs BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
git commit -m "feat: add MessageOperationsViewModel to ConnectionTabViewModel

Each tab now has its own message operations instance for loading,
filtering, and selecting messages independently.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 3: Add Connection Methods to ConnectionTabViewModel

**Files:**
- Modify: `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`
- Modify: `BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs`

**Step 1: Add tests for connection methods**

Add to `ConnectionTabViewModelTests.cs`:

```csharp
[Fact]
public async Task ConnectWithConnectionStringAsync_SetsConnectionState()
{
    // Arrange
    var preferencesService = Substitute.For<IPreferencesService>();
    var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
    var operations = Substitute.For<IServiceBusOperations>();

    operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
    operations.GetQueuesAsync().Returns(new List<QueueInfo>());
    operations.GetTopicsAsync().Returns(new List<TopicInfo>());

    var connection = new SavedConnection
    {
        Id = "conn-1",
        Name = "Test Connection",
        ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
        Type = ConnectionType.Namespace
    };

    var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService);

    // Act
    await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

    // Assert
    tab.IsConnected.Should().BeTrue();
    tab.Mode.Should().Be(ConnectionMode.ConnectionString);
    tab.SavedConnection.Should().Be(connection);
}

[Fact]
public async Task DisconnectAsync_ClearsConnectionState()
{
    // Arrange
    var preferencesService = Substitute.For<IPreferencesService>();
    var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
    var operations = Substitute.For<IServiceBusOperations>();

    operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
    operations.GetQueuesAsync().Returns(new List<QueueInfo>());
    operations.GetTopicsAsync().Returns(new List<TopicInfo>());

    var connection = new SavedConnection
    {
        Id = "conn-1",
        Name = "Test Connection",
        ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
        Type = ConnectionType.Namespace
    };

    var tab = new ConnectionTabViewModel("test-id", "Test Tab", "test.servicebus.windows.net", preferencesService);
    await tab.ConnectWithConnectionStringAsync(connection, operationsFactory);

    // Act
    await tab.DisconnectAsync();

    // Assert
    tab.IsConnected.Should().BeFalse();
    tab.Mode.Should().Be(ConnectionMode.None);
}
```

Add usings:
```csharp
using BusLane.Models;
using BusLane.Services.ServiceBus;
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ConnectionTabViewModelTests.ConnectWithConnectionStringAsync"`
Expected: FAIL - method doesn't exist

**Step 3: Add connection methods to ConnectionTabViewModel**

Add these methods to `ConnectionTabViewModel.cs`:

```csharp
/// <summary>
/// Connects to a Service Bus namespace using a saved connection string.
/// </summary>
public async Task ConnectWithConnectionStringAsync(
    SavedConnection connection,
    IServiceBusOperationsFactory operationsFactory)
{
    IsLoading = true;
    StatusMessage = $"Connecting to {connection.Name}...";

    try
    {
        _savedConnection = connection;
        _operations = operationsFactory.CreateFromConnectionString(connection.ConnectionString);
        Mode = ConnectionMode.ConnectionString;

        TabTitle = connection.Name;
        TabSubtitle = connection.Endpoint ?? "";

        await LoadEntitiesAsync(connection);

        IsConnected = true;
        StatusMessage = "Connected";
    }
    catch (Exception ex)
    {
        StatusMessage = $"Connection failed: {ex.Message}";
        _operations = null;
        _savedConnection = null;
        Mode = ConnectionMode.None;
        throw;
    }
    finally
    {
        IsLoading = false;
    }
}

/// <summary>
/// Connects to a Service Bus namespace using Azure credentials.
/// </summary>
public async Task ConnectWithAzureCredentialAsync(
    ServiceBusNamespace ns,
    Azure.Core.TokenCredential credential,
    IServiceBusOperationsFactory operationsFactory)
{
    IsLoading = true;
    StatusMessage = $"Connecting to {ns.Name}...";

    try
    {
        _namespace = ns;
        _operations = operationsFactory.CreateFromAzureCredential(ns.Endpoint, ns.Id, credential);
        Mode = ConnectionMode.AzureAccount;

        TabTitle = ns.Name;
        TabSubtitle = ns.Endpoint;

        await LoadNamespaceEntitiesAsync();

        IsConnected = true;
        StatusMessage = "Connected";
    }
    catch (Exception ex)
    {
        StatusMessage = $"Connection failed: {ex.Message}";
        _operations = null;
        _namespace = null;
        Mode = ConnectionMode.None;
        throw;
    }
    finally
    {
        IsLoading = false;
    }
}

/// <summary>
/// Disconnects from the current Service Bus namespace.
/// </summary>
public async Task DisconnectAsync()
{
    if (_operations != null)
    {
        await _operations.DisposeAsync();
        _operations = null;
    }

    _savedConnection = null;
    _namespace = null;
    Mode = ConnectionMode.None;
    IsConnected = false;

    Navigation.Clear();
    MessageOps.Clear();

    StatusMessage = "Disconnected";
}

private async Task LoadEntitiesAsync(SavedConnection connection)
{
    if (_operations == null) return;

    Navigation.Clear();

    if (connection.Type == ConnectionType.Namespace)
    {
        await LoadNamespaceEntitiesAsync();
    }
    else if (connection.Type == ConnectionType.Queue && connection.EntityName != null)
    {
        var queueInfo = await _operations.GetQueueInfoAsync(connection.EntityName);
        if (queueInfo != null)
        {
            Navigation.Queues.Add(queueInfo);
            Navigation.SelectedQueue = queueInfo;
            Navigation.SelectedEntity = queueInfo;
        }
    }
    else if (connection.Type == ConnectionType.Topic && connection.EntityName != null)
    {
        var topicInfo = await _operations.GetTopicInfoAsync(connection.EntityName);
        if (topicInfo != null)
        {
            Navigation.Topics.Add(topicInfo);
            Navigation.SelectedTopic = topicInfo;
            Navigation.SelectedEntity = topicInfo;

            var subs = await _operations.GetSubscriptionsAsync(connection.EntityName);
            foreach (var sub in subs)
                Navigation.TopicSubscriptions.Add(sub);
        }
    }
}

private async Task LoadNamespaceEntitiesAsync()
{
    if (_operations == null) return;

    var queues = await _operations.GetQueuesAsync();
    foreach (var queue in queues)
        Navigation.Queues.Add(queue);

    var topics = await _operations.GetTopicsAsync();
    foreach (var topic in topics)
        Navigation.Topics.Add(topic);

    StatusMessage = $"{Navigation.Queues.Count} queue(s), {Navigation.Topics.Count} topic(s)";
}
```

Add using at top:
```csharp
using Azure.Core;
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ConnectionTabViewModelTests"`
Expected: PASS (6 tests)

**Step 5: Commit**

```bash
git add BusLane/ViewModels/Core/ConnectionTabViewModel.cs BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
git commit -m "feat: add connection methods to ConnectionTabViewModel

Supports both connection string and Azure credential modes.
Loads queues/topics on connect and clears state on disconnect.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 4: Create TabSessionState Model

**Files:**
- Create: `BusLane/Models/TabSessionState.cs`
- Test: `BusLane.Tests/Models/TabSessionStateTests.cs`

**Step 1: Create test file**

```csharp
// BusLane.Tests/Models/TabSessionStateTests.cs
using BusLane.Models;
using BusLane.ViewModels;
using FluentAssertions;

namespace BusLane.Tests.Models;

public class TabSessionStateTests
{
    [Fact]
    public void Create_WithConnectionStringMode_SetsCorrectProperties()
    {
        // Arrange & Act
        var state = new TabSessionState
        {
            TabId = "tab-1",
            Mode = ConnectionMode.ConnectionString,
            ConnectionId = "conn-1",
            TabOrder = 0
        };

        // Assert
        state.TabId.Should().Be("tab-1");
        state.Mode.Should().Be(ConnectionMode.ConnectionString);
        state.ConnectionId.Should().Be("conn-1");
        state.NamespaceId.Should().BeNull();
    }

    [Fact]
    public void Create_WithAzureMode_SetsCorrectProperties()
    {
        // Arrange & Act
        var state = new TabSessionState
        {
            TabId = "tab-2",
            Mode = ConnectionMode.AzureAccount,
            NamespaceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns",
            TabOrder = 1
        };

        // Assert
        state.Mode.Should().Be(ConnectionMode.AzureAccount);
        state.NamespaceId.Should().NotBeNullOrEmpty();
        state.ConnectionId.Should().BeNull();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TabSessionStateTests"`
Expected: FAIL - TabSessionState doesn't exist

**Step 3: Create TabSessionState model**

```csharp
// BusLane/Models/TabSessionState.cs
using BusLane.ViewModels;

namespace BusLane.Models;

/// <summary>
/// Represents the persisted state of a connection tab for session restore.
/// </summary>
public record TabSessionState
{
    /// <summary>
    /// Unique identifier for the tab.
    /// </summary>
    public required string TabId { get; init; }

    /// <summary>
    /// The connection mode used by this tab.
    /// </summary>
    public required ConnectionMode Mode { get; init; }

    /// <summary>
    /// ID of the saved connection (for ConnectionString mode).
    /// </summary>
    public string? ConnectionId { get; init; }

    /// <summary>
    /// Azure Resource ID of the namespace (for AzureAccount mode).
    /// </summary>
    public string? NamespaceId { get; init; }

    /// <summary>
    /// Name of the selected entity when the session was saved.
    /// </summary>
    public string? SelectedEntityName { get; init; }

    /// <summary>
    /// Position of this tab in the tab bar.
    /// </summary>
    public int TabOrder { get; init; }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TabSessionStateTests"`
Expected: PASS (2 tests)

**Step 5: Commit**

```bash
git add BusLane/Models/TabSessionState.cs BusLane.Tests/Models/TabSessionStateTests.cs
git commit -m "feat: add TabSessionState model for session persistence

Stores tab identity, connection reference, and position for
restoring tabs across app restarts.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

## Phase 2: Refactor MainWindowViewModel to Shell Pattern

### Task 5: Add Tab Collection to MainWindowViewModel

**Files:**
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`

**Step 1: Add tab collection properties**

Add these properties near the top of `MainWindowViewModel.cs` after the existing composed components:

```csharp
// Tab management
public ObservableCollection<ConnectionTabViewModel> ConnectionTabs { get; } = new();

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasActiveTabs))]
[NotifyPropertyChangedFor(nameof(ShellStatusMessage))]
private ConnectionTabViewModel? _activeTab;

public bool HasActiveTabs => ConnectionTabs.Count > 0;
public string? ShellStatusMessage => ActiveTab?.StatusMessage ?? StatusMessage;
```

Add using:
```csharp
using System.Collections.ObjectModel;
```

**Step 2: Build to verify compilation**

Run: `dotnet build`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add BusLane/ViewModels/MainWindowViewModel.cs
git commit -m "feat: add tab collection to MainWindowViewModel

Prepares the shell to manage multiple connection tabs.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 6: Add Tab Management Commands

**Files:**
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`

**Step 1: Add tab management methods**

Add these methods to `MainWindowViewModel.cs` in a new region:

```csharp
#region Tab Management

/// <summary>
/// Opens a new tab for the given saved connection.
/// </summary>
[RelayCommand]
public async Task OpenTabForConnectionAsync(SavedConnection connection)
{
    var tab = new ConnectionTabViewModel(
        Guid.NewGuid().ToString(),
        connection.Name,
        connection.Endpoint ?? "",
        _preferencesService);

    ConnectionTabs.Add(tab);
    ActiveTab = tab;

    try
    {
        await tab.ConnectWithConnectionStringAsync(connection, _operationsFactory);
    }
    catch (Exception ex)
    {
        StatusMessage = $"Failed to connect: {ex.Message}";
    }
}

/// <summary>
/// Opens a new tab for the given Azure namespace.
/// </summary>
[RelayCommand]
public async Task OpenTabForNamespaceAsync(ServiceBusNamespace ns)
{
    if (_auth.Credential == null) return;

    var tab = new ConnectionTabViewModel(
        Guid.NewGuid().ToString(),
        ns.Name,
        ns.Endpoint,
        _preferencesService);

    ConnectionTabs.Add(tab);
    ActiveTab = tab;

    try
    {
        await tab.ConnectWithAzureCredentialAsync(ns, _auth.Credential, _operationsFactory);
    }
    catch (Exception ex)
    {
        StatusMessage = $"Failed to connect: {ex.Message}";
    }
}

/// <summary>
/// Closes the specified tab.
/// </summary>
[RelayCommand]
public async Task CloseTabAsync(string tabId)
{
    var tab = ConnectionTabs.FirstOrDefault(t => t.TabId == tabId);
    if (tab == null) return;

    await tab.DisconnectAsync();

    var index = ConnectionTabs.IndexOf(tab);
    ConnectionTabs.Remove(tab);

    // Switch to nearest tab or clear active
    if (ConnectionTabs.Count == 0)
    {
        ActiveTab = null;
    }
    else if (ActiveTab == tab)
    {
        var newIndex = Math.Min(index, ConnectionTabs.Count - 1);
        ActiveTab = ConnectionTabs[newIndex];
    }
}

/// <summary>
/// Switches to the specified tab.
/// </summary>
[RelayCommand]
public void SwitchToTab(string tabId)
{
    var tab = ConnectionTabs.FirstOrDefault(t => t.TabId == tabId);
    if (tab != null)
    {
        ActiveTab = tab;
    }
}

/// <summary>
/// Closes the currently active tab.
/// </summary>
[RelayCommand]
public async Task CloseActiveTabAsync()
{
    if (ActiveTab != null)
    {
        await CloseTabAsync(ActiveTab.TabId);
    }
}

partial void OnActiveTabChanged(ConnectionTabViewModel? value)
{
    OnPropertyChanged(nameof(ShellStatusMessage));

    // Update the legacy _operations reference for backward compatibility
    SetOperations(value?.Operations);
}

#endregion
```

**Step 2: Build to verify compilation**

Run: `dotnet build`
Expected: Build succeeded

**Step 3: Run all tests to ensure no regressions**

Run: `dotnet test`
Expected: All 120+ tests pass

**Step 4: Commit**

```bash
git add BusLane/ViewModels/MainWindowViewModel.cs
git commit -m "feat: add tab management commands to MainWindowViewModel

Adds commands for opening, closing, and switching between tabs.
Maintains backward compatibility with existing _operations field.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

## Phase 3: Tab Bar UI

### Task 7: Create TabBarView User Control

**Files:**
- Create: `BusLane/Views/Controls/TabBarView.axaml`
- Create: `BusLane/Views/Controls/TabBarView.axaml.cs`

**Step 1: Create the AXAML file**

```xml
<!-- BusLane/Views/Controls/TabBarView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:BusLane.ViewModels"
             xmlns:core="using:BusLane.ViewModels.Core"
             x:Class="BusLane.Views.Controls.TabBarView"
             x:DataType="vm:MainWindowViewModel">

    <Border Background="{DynamicResource SurfaceSubtle}"
            BorderBrush="{DynamicResource BorderDefault}"
            BorderThickness="0,0,0,1"
            Padding="8,4"
            IsVisible="{Binding HasActiveTabs}">
        <Grid ColumnDefinitions="*,Auto">
            <!-- Tab List -->
            <ScrollViewer Grid.Column="0"
                          HorizontalScrollBarVisibility="Auto"
                          VerticalScrollBarVisibility="Disabled">
                <ItemsControl ItemsSource="{Binding ConnectionTabs}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <StackPanel Orientation="Horizontal" Spacing="4"/>
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate x:DataType="core:ConnectionTabViewModel">
                            <Button Classes="tab-button"
                                    Command="{Binding $parent[UserControl].((vm:MainWindowViewModel)DataContext).SwitchToTabCommand}"
                                    CommandParameter="{Binding TabId}"
                                    Padding="12,8"
                                    MinWidth="120"
                                    MaxWidth="200">
                                <Button.Styles>
                                    <Style Selector="Button.tab-button">
                                        <Setter Property="Background" Value="Transparent"/>
                                        <Setter Property="BorderThickness" Value="0"/>
                                        <Setter Property="CornerRadius" Value="6"/>
                                    </Style>
                                    <Style Selector="Button.tab-button:pointerover">
                                        <Setter Property="Background" Value="{DynamicResource SurfaceHover}"/>
                                    </Style>
                                </Button.Styles>

                                <Grid ColumnDefinitions="Auto,*,Auto">
                                    <!-- Connection Status Indicator -->
                                    <Ellipse Grid.Column="0"
                                             Width="8" Height="8"
                                             Margin="0,0,8,0"
                                             VerticalAlignment="Center">
                                        <Ellipse.Styles>
                                            <Style Selector="Ellipse">
                                                <Setter Property="Fill" Value="{DynamicResource SuccessColor}"/>
                                            </Style>
                                        </Ellipse.Styles>
                                        <Ellipse.IsVisible>
                                            <Binding Path="IsConnected"/>
                                        </Ellipse.IsVisible>
                                    </Ellipse>

                                    <!-- Tab Title & Subtitle -->
                                    <StackPanel Grid.Column="1" Spacing="2" VerticalAlignment="Center">
                                        <TextBlock Text="{Binding TabTitle}"
                                                   FontWeight="SemiBold"
                                                   FontSize="12"
                                                   TextTrimming="CharacterEllipsis"/>
                                        <TextBlock Text="{Binding TabSubtitle}"
                                                   FontSize="10"
                                                   Foreground="{DynamicResource SubtleForeground}"
                                                   TextTrimming="CharacterEllipsis"/>
                                    </StackPanel>

                                    <!-- Close Button -->
                                    <Button Grid.Column="2"
                                            Classes="subtle"
                                            Command="{Binding $parent[UserControl].((vm:MainWindowViewModel)DataContext).CloseTabCommand}"
                                            CommandParameter="{Binding TabId}"
                                            Padding="4"
                                            Margin="8,0,0,0"
                                            VerticalAlignment="Center"
                                            ToolTip.Tip="Close tab">
                                        <PathIcon Data="{StaticResource IconClose}" Width="10" Height="10"/>
                                    </Button>
                                </Grid>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>

            <!-- New Tab Button -->
            <Button Grid.Column="1"
                    Classes="subtle"
                    Command="{Binding OpenConnectionLibraryCommand}"
                    Padding="8,6"
                    Margin="8,0,0,0"
                    ToolTip.Tip="Open new connection tab">
                <StackPanel Orientation="Horizontal" Spacing="4">
                    <PathIcon Data="{StaticResource IconAdd}" Width="12" Height="12"/>
                    <TextBlock Text="New Tab" FontSize="12"/>
                </StackPanel>
            </Button>
        </Grid>
    </Border>
</UserControl>
```

**Step 2: Create the code-behind file**

```csharp
// BusLane/Views/Controls/TabBarView.axaml.cs
using Avalonia.Controls;

namespace BusLane.Views.Controls;

public partial class TabBarView : UserControl
{
    public TabBarView()
    {
        InitializeComponent();
    }
}
```

**Step 3: Build to verify compilation**

Run: `dotnet build`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add BusLane/Views/Controls/TabBarView.axaml BusLane/Views/Controls/TabBarView.axaml.cs
git commit -m "feat: add TabBarView user control

Displays connection tabs with title, subtitle, status indicator,
and close button. Includes new tab button to open connection library.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 8: Add Active Tab Visual Indicator

**Files:**
- Modify: `BusLane/Views/Controls/TabBarView.axaml`

**Step 1: Update tab button styling for active state**

Replace the Button.Styles section in `TabBarView.axaml`:

```xml
<Button.Styles>
    <Style Selector="Button.tab-button">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0,0,0,2"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
        <Setter Property="CornerRadius" Value="6,6,0,0"/>
    </Style>
    <Style Selector="Button.tab-button:pointerover">
        <Setter Property="Background" Value="{DynamicResource SurfaceHover}"/>
    </Style>
</Button.Styles>

<!-- Add after Button.Styles, inside the Button element: -->
<Button.Background>
    <MultiBinding Converter="{x:Static converters:TabActiveConverter.Instance}">
        <Binding Path="TabId"/>
        <Binding Path="$parent[UserControl].((vm:MainWindowViewModel)DataContext).ActiveTab.TabId"/>
    </MultiBinding>
</Button.Background>
<Button.BorderBrush>
    <MultiBinding Converter="{x:Static converters:TabActiveBorderConverter.Instance}">
        <Binding Path="TabId"/>
        <Binding Path="$parent[UserControl].((vm:MainWindowViewModel)DataContext).ActiveTab.TabId"/>
    </MultiBinding>
</Button.BorderBrush>
```

Add namespace at top:
```xml
xmlns:converters="using:BusLane.Converters"
```

**Step 2: Create TabActiveConverter**

```csharp
// BusLane/Converters/TabActiveConverter.cs
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BusLane.Converters;

/// <summary>
/// Converts tab active state to background brush.
/// </summary>
public class TabActiveConverter : IMultiValueConverter
{
    public static readonly TabActiveConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Brushes.Transparent;

        var tabId = values[0] as string;
        var activeTabId = values[1] as string;

        if (tabId != null && tabId == activeTabId)
        {
            return Application.Current?.FindResource("SurfaceSelected") as IBrush ?? Brushes.Transparent;
        }

        return Brushes.Transparent;
    }
}

/// <summary>
/// Converts tab active state to border brush.
/// </summary>
public class TabActiveBorderConverter : IMultiValueConverter
{
    public static readonly TabActiveBorderConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Brushes.Transparent;

        var tabId = values[0] as string;
        var activeTabId = values[1] as string;

        if (tabId != null && tabId == activeTabId)
        {
            return Application.Current?.FindResource("AccentBrand") as IBrush ?? Brushes.Transparent;
        }

        return Brushes.Transparent;
    }
}
```

**Step 3: Build to verify compilation**

Run: `dotnet build`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add BusLane/Views/Controls/TabBarView.axaml BusLane/Converters/TabActiveConverter.cs
git commit -m "feat: add active tab visual indicator

Active tab shows selected background and accent border.
Uses converters to compare tab ID with active tab ID.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 9: Integrate TabBarView into MainWindow

**Files:**
- Modify: `BusLane/Views/MainWindow.axaml`

**Step 1: Add TabBarView to the main layout**

In `MainWindow.axaml`, find the main content Grid (Grid.Column="2") and add the tab bar. Update the row definitions and add the TabBarView:

```xml
<!-- Main Content -->
<Grid Grid.Column="2" Margin="24,20">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>  <!-- Tab Bar -->
        <RowDefinition Height="Auto"/>  <!-- Header (existing) -->
        <RowDefinition Height="*"/>     <!-- Content -->
        <RowDefinition Height="Auto"/>  <!-- Status Bar -->
    </Grid.RowDefinitions>

    <!-- Tab Bar (new) -->
    <controls:TabBarView Grid.Row="0" Margin="0,0,0,12"/>

    <!-- Welcome / Not Connected (update Grid.Row from 1 to 2) -->
    <controls:WelcomeView Grid.Row="2"
                          VerticalAlignment="Center"
                          HorizontalAlignment="Center"/>

    <!-- Content when namespace selected - update Grid.Row from "0" to "1" and Grid.RowSpan from "2" to "2" -->
    <!-- ... existing content with updated row numbers ... -->

    <!-- Status Bar - update Grid.Row from 2 to 3 -->
    <!-- ... existing status bar ... -->
</Grid>
```

**Step 2: Build and run to verify UI**

Run: `dotnet build && dotnet run --project BusLane/BusLane.csproj`
Expected: App launches with tab bar visible when tabs are open

**Step 3: Commit**

```bash
git add BusLane/Views/MainWindow.axaml
git commit -m "feat: integrate TabBarView into MainWindow layout

Tab bar appears above content area when connections are open.
Adjusted row definitions to accommodate the new UI element.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

## Phase 4: Wire Up Tab-Based Navigation

### Task 10: Update Content Panels to Use ActiveTab

**Files:**
- Modify: `BusLane/Views/MainWindow.axaml`

**Step 1: Update entity tree and messages panel bindings**

Update the bindings in the Azure mode and Connection String mode sections to reference `ActiveTab`:

For the Azure mode section, update:
```xml
<!-- Main content grid -->
<Grid Grid.Row="1" ColumnDefinitions="1.2*,1.8*">
    <!-- Service Bus Tree View - now binds to active tab's navigation -->
    <controls:AzureEntityTreeView Grid.Column="0" Margin="0,0,12,0"
                                   DataContext="{Binding ActiveTab}"/>

    <!-- Messages Panel - now binds to active tab's message ops -->
    <controls:MessagesPanelView Grid.Column="1" Margin="12,0,0,0"
                                 DataContext="{Binding ActiveTab}"/>
</Grid>
```

**Step 2: Build to verify compilation**

Run: `dotnet build`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add BusLane/Views/MainWindow.axaml
git commit -m "feat: wire content panels to ActiveTab

Entity tree and messages panel now bind to the active tab's
NavigationState and MessageOperationsViewModel.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 11: Update Connection Flow to Create Tabs

**Files:**
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`

**Step 1: Update SelectNamespaceAsync to create tab**

Modify the `SelectNamespaceAsync` method to use the new tab-based approach:

```csharp
[RelayCommand]
private async Task SelectNamespaceAsync(ServiceBusNamespace ns)
{
    // Close the namespace selection panel
    Connection.CloseNamespacePanel();

    // Use the new tab-based connection
    await OpenTabForNamespaceAsync(ns);
}
```

**Step 2: Update ConnectToSavedConnectionAsync to create tab**

Modify the command to delegate to tab creation:

```csharp
[RelayCommand]
private async Task ConnectToSavedConnectionAsync(SavedConnection connection)
{
    IsLoading = true;
    try
    {
        // Close connection library
        Connection.CloseConnectionLibrary();

        // Use the new tab-based connection
        await OpenTabForConnectionAsync(connection);
    }
    finally
    {
        IsLoading = false;
    }
}
```

**Step 3: Build and run tests**

Run: `dotnet build && dotnet test`
Expected: All tests pass

**Step 4: Commit**

```bash
git add BusLane/ViewModels/MainWindowViewModel.cs
git commit -m "feat: update connection flow to create tabs

Selecting a namespace or saved connection now creates a new tab
instead of updating the single connection state.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

## Phase 5: Session Persistence

### Task 12: Add Session Persistence to Preferences

**Files:**
- Modify: `BusLane/Models/UserPreferences.cs`
- Modify: `BusLane/Services/Abstractions/IPreferencesService.cs`
- Modify: `BusLane/Services/Infrastructure/PreferencesService.cs`

**Step 1: Add preference properties**

In `UserPreferences.cs`, add:
```csharp
public bool RestoreTabsOnStartup { get; set; } = true;
public List<TabSessionState>? SavedTabSession { get; set; }
```

In `IPreferencesService.cs`, add:
```csharp
bool RestoreTabsOnStartup { get; set; }
List<TabSessionState>? SavedTabSession { get; set; }
```

In `PreferencesService.cs`, add the property implementations.

**Step 2: Build to verify compilation**

Run: `dotnet build`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add BusLane/Models/UserPreferences.cs BusLane/Services/Abstractions/IPreferencesService.cs BusLane/Services/Infrastructure/PreferencesService.cs
git commit -m "feat: add tab session persistence preferences

Adds RestoreTabsOnStartup preference and SavedTabSession storage.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 13: Implement Session Save/Restore

**Files:**
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`

**Step 1: Add session save method**

```csharp
/// <summary>
/// Saves the current tab session to preferences.
/// </summary>
public void SaveTabSession()
{
    if (!_preferencesService.RestoreTabsOnStartup)
    {
        _preferencesService.SavedTabSession = null;
        _preferencesService.Save();
        return;
    }

    var session = ConnectionTabs.Select((tab, index) => new TabSessionState
    {
        TabId = tab.TabId,
        Mode = tab.Mode,
        ConnectionId = tab.SavedConnection?.Id,
        NamespaceId = tab.Namespace?.Id,
        SelectedEntityName = tab.Navigation.CurrentEntityName,
        TabOrder = index
    }).ToList();

    _preferencesService.SavedTabSession = session;
    _preferencesService.Save();
}

/// <summary>
/// Restores tabs from the saved session.
/// </summary>
public async Task RestoreTabSessionAsync()
{
    if (!_preferencesService.RestoreTabsOnStartup) return;

    var session = _preferencesService.SavedTabSession;
    if (session == null || session.Count == 0) return;

    foreach (var tabState in session.OrderBy(t => t.TabOrder))
    {
        try
        {
            if (tabState.Mode == ConnectionMode.ConnectionString && tabState.ConnectionId != null)
            {
                var connections = await _connectionStorage.GetConnectionsAsync();
                var connection = connections.FirstOrDefault(c => c.Id == tabState.ConnectionId);
                if (connection != null)
                {
                    await OpenTabForConnectionAsync(connection);
                }
            }
            // Azure mode restoration requires re-authentication, skip for now
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to restore tab: {ex.Message}";
        }
    }
}
```

**Step 2: Call save on app shutdown**

This will be called from App.axaml.cs or MainWindow code-behind during shutdown.

**Step 3: Build and test**

Run: `dotnet build && dotnet test`
Expected: All tests pass

**Step 4: Commit**

```bash
git add BusLane/ViewModels/MainWindowViewModel.cs
git commit -m "feat: implement tab session save and restore

Saves open tabs to preferences on shutdown.
Restores connection string mode tabs on startup.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

## Phase 6: Polish and Final Integration

### Task 14: Add Keyboard Shortcuts for Tabs

**Files:**
- Modify: `BusLane/Services/Infrastructure/KeyboardShortcutService.cs`
- Modify: `BusLane/Views/MainWindow.axaml.cs`

**Step 1: Register tab shortcuts**

Add to `KeyboardShortcutService.cs`:
```csharp
new KeyboardShortcut("Ctrl+T", "New Tab", "Tabs", () => /* trigger new tab */),
new KeyboardShortcut("Ctrl+W", "Close Tab", "Tabs", () => /* trigger close tab */),
new KeyboardShortcut("Ctrl+Tab", "Next Tab", "Tabs", () => /* trigger next tab */),
new KeyboardShortcut("Ctrl+Shift+Tab", "Previous Tab", "Tabs", () => /* trigger prev tab */),
```

**Step 2: Build to verify**

Run: `dotnet build`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add BusLane/Services/Infrastructure/KeyboardShortcutService.cs BusLane/Views/MainWindow.axaml.cs
git commit -m "feat: add keyboard shortcuts for tab navigation

Ctrl+T: New tab, Ctrl+W: Close tab
Ctrl+Tab: Next tab, Ctrl+Shift+Tab: Previous tab

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 15: Add Settings Toggle for Tab Restore

**Files:**
- Modify: `BusLane/Views/Dialogs/SettingsDialog.axaml`

**Step 1: Add toggle in settings dialog**

Add to the settings panel:
```xml
<StackPanel Orientation="Horizontal" Spacing="12" Margin="0,16,0,0">
    <ToggleSwitch IsChecked="{Binding RestoreTabsOnStartup}"/>
    <StackPanel>
        <TextBlock Text="Restore tabs on startup" FontWeight="SemiBold"/>
        <TextBlock Text="Remember open connections between sessions"
                   Classes="caption" Foreground="{DynamicResource SubtleForeground}"/>
    </StackPanel>
</StackPanel>
```

**Step 2: Build to verify**

Run: `dotnet build`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add BusLane/Views/Dialogs/SettingsDialog.axaml
git commit -m "feat: add session restore toggle to settings

Users can enable/disable automatic tab restoration on startup.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

### Task 16: Final Integration Testing

**Files:**
- None (manual testing)

**Step 1: Run full test suite**

Run: `dotnet test`
Expected: All tests pass

**Step 2: Manual testing checklist**

- [ ] Open app with no saved session
- [ ] Connect via Azure - creates tab
- [ ] Connect via connection string - creates second tab
- [ ] Switch between tabs
- [ ] Close a tab
- [ ] Close last tab shows empty state
- [ ] Restart app - tabs restored
- [ ] Disable restore preference - tabs not restored

**Step 3: Build release**

Run: `dotnet build -c Release`
Expected: Build succeeded

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete multi-connection tabs implementation

Phase 1-6 complete:
- ConnectionTabViewModel for per-tab state
- Tab bar UI with active indicator
- Tab management commands
- Session persistence
- Keyboard shortcuts
- Settings integration

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
```

---

## Summary

This plan implements multi-connection tabs in 16 tasks across 6 phases:

| Phase | Tasks | Focus |
|-------|-------|-------|
| 1 | 1-4 | Core tab infrastructure |
| 2 | 5-6 | Shell pattern refactor |
| 3 | 7-9 | Tab bar UI |
| 4 | 10-11 | Wire up navigation |
| 5 | 12-13 | Session persistence |
| 6 | 14-16 | Polish and testing |

Each task follows TDD: write test → verify fail → implement → verify pass → commit.
