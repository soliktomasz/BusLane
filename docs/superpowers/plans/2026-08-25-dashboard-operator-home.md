# Dashboard Operator Home Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn namespace dashboard from blocking overlay into exception-first namespace home that navigates visibly into existing Service Bus workspaces.

**Architecture:** Store Overview versus Entity mode on each `ConnectionTabViewModel`, render dashboard as normal namespace content, and route dashboard intent through typed navigation requests handled by `MainWindowViewModel`. Keep existing message/session components, split actionable priority from full issue inventory, and add focused search/quick-access view models around existing entity data.

**Tech Stack:** .NET 10, C#, Avalonia XAML, CommunityToolkit.Mvvm, xUnit, FluentAssertions, NSubstitute.

**Spec:** `docs/superpowers/specs/2026-08-25-dashboard-operator-home-design.md`

## Global Constraints

- One connection tab represents one Service Bus namespace; normal entity navigation never creates another tab.
- Dashboard refresh runs only while active tab shows Overview.
- Global search covers queues, topics, and subscriptions only; it never peeks messages.
- Queue/subscription default search destination is Active Messages; topic default is Topic Subscriptions.
- Priority Work shows at most eight actionable, unreviewed entities.
- Dashboard primary actions are always visible and keyboard accessible; no hover-only critical actions.
- Reuse `IServiceBusOperations`; do not introduce legacy Service Bus services.
- Preserve existing Active, DLQ, Sessions, bulk operations, and message-loading behavior.
- Nullable reference types stay enabled; async methods end in `Async`.
- Every task follows red-green-refactor and ends with focused tests plus a commit.

---

## File and Responsibility Map

**Create**

- `BusLane/Models/Dashboard/NamespaceWorkspaceNavigation.cs`: workspace, overview section, destination enums, typed navigation request, and recent destination value.
- `BusLane/Models/Dashboard/NamespaceEntitySearchResult.cs`: immutable search result with display path and destination request.
- `BusLane/ViewModels/Dashboard/NamespaceEntitySearchViewModel.cs`: in-memory grouped fuzzy search and keyboard selection state.
- `BusLane/Views/Controls/NamespaceWorkspaceBreadcrumb.axaml`: visible Overview/entity path and return command.
- `BusLane/Views/Controls/NamespaceWorkspaceBreadcrumb.axaml.cs`: Avalonia control initialization only.
- `BusLane/Views/Controls/NamespaceEntitySearchView.axaml`: virtualized search-result UI.
- `BusLane/Views/Controls/NamespaceEntitySearchView.axaml.cs`: Avalonia control initialization only.
- `BusLane/Views/Controls/NamespaceIssuesView.axaml`: virtualized full issue inventory.
- `BusLane/Views/Controls/NamespaceIssuesView.axaml.cs`: Avalonia control initialization only.
- `BusLane.Tests/ViewModels/Dashboard/NamespaceEntitySearchViewModelTests.cs`: search ranking and routing contracts.

**Modify**

- `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`: per-tab workspace/overview state and bounded recent history.
- `BusLane/ViewModels/MainWindowViewModel.cs`: visible workspace selection, typed destination coordinator, overview lifecycle, and recent recording.
- `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs`: remove dashboard overlay ownership only.
- `BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs`: overview sections, search/navigation context, refresh state, health projection.
- `BusLane/ViewModels/Dashboard/NamespaceInboxViewModel.cs`: priority/full projections and review suppression.
- `BusLane/ViewModels/Dashboard/NamespaceInboxItemViewModel.cs`: typed action intent and reviewed state.
- `BusLane/Services/Dashboard/IDashboardRefreshService.cs`: scoped refresh-failure notification.
- `BusLane/Services/Dashboard/DashboardRefreshService.cs`: preserve successful sections and publish scoped failures.
- `BusLane/Views/MainWindow.axaml`: dashboard as normal content, entity workspace switch, breadcrumb, remove chart overlay.
- `BusLane/Views/Controls/NavigationSidebar.axaml`: relabel Dashboard entry as Overview.
- `BusLane/Views/Controls/NamespaceDashboardView.axaml`: approved Triage Home layout and Overview sub-sections.
- `BusLane/Views/Controls/NamespaceInboxView.axaml`: always-visible contextual action and virtualized list.
- `BusLane/Styles/AppStyles.axaml`: operator-home, search, breadcrumb, issue, and state styles using existing tokens.
- Existing tests under `BusLane.Tests/ViewModels`, `BusLane.Tests/Services/Dashboard`, and `BusLane.Tests/Views`.

---

### Task 1: Per-tab workspace state and typed navigation vocabulary

**Files:**
- Create: `BusLane/Models/Dashboard/NamespaceWorkspaceNavigation.cs`
- Modify: `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`
- Test: `BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs`

**Interfaces:**
- Consumes: existing `EntityType`, `ConnectionTabViewModel`, and CommunityToolkit observable properties.
- Produces: `NamespaceWorkspaceMode`, `NamespaceOverviewSection`, `EntityWorkspaceView`, `NamespaceNavigationRequest`, `RecentEntityDestination`, `ConnectionTabViewModel.WorkspaceMode`, `ConnectionTabViewModel.OverviewSection`, `ConnectionTabViewModel.RecentDestinations`, and `RecordRecentDestination`.

- [ ] **Step 1: Write failing defaults and recent-history tests**

Add tests proving new tabs default to Overview and recent history deduplicates by entity plus destination while keeping newest five:

```csharp
[Fact]
public void Constructor_DefaultsToOverviewHome()
{
    var tab = new ConnectionTabViewModel("id", "Title", "namespace");

    tab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Overview);
    tab.OverviewSection.Should().Be(NamespaceOverviewSection.Home);
}

[Fact]
public void RecordRecentDestination_DeduplicatesAndCapsAtFive()
{
    var tab = new ConnectionTabViewModel("id", "Title", "namespace");
    for (var index = 0; index < 6; index++)
    {
        tab.RecordRecentDestination(new NamespaceNavigationRequest(
            EntityType.Queue,
            $"queue-{index}",
            TopicName: null,
            EntityWorkspaceView.ActiveMessages));
    }

    tab.RecordRecentDestination(new NamespaceNavigationRequest(
        EntityType.Queue,
        "queue-3",
        TopicName: null,
        EntityWorkspaceView.ActiveMessages));

    tab.RecentDestinations.Should().HaveCount(5);
    tab.RecentDestinations[0].Request.EntityName.Should().Be("queue-3");
    tab.RecentDestinations.Select(item => item.Request.EntityName).Should().OnlyHaveUniqueItems();
}
```

- [ ] **Step 2: Run focused tests and verify red**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~ConnectionTabViewModelTests"
```

Expected: compile failure because workspace types and properties do not exist.

- [ ] **Step 3: Add navigation value types**

Create:

```csharp
namespace BusLane.Models.Dashboard;

public enum NamespaceWorkspaceMode { Overview, Entity }
public enum NamespaceOverviewSection { Home, Issues, Analytics }
public enum EntityWorkspaceView { ActiveMessages, DeadLetters, Sessions, TopicSubscriptions }

public sealed record NamespaceNavigationRequest(
    EntityType EntityType,
    string EntityName,
    string? TopicName,
    EntityWorkspaceView View);

public sealed record RecentEntityDestination(
    NamespaceNavigationRequest Request,
    DateTimeOffset OpenedAt);
```

- [ ] **Step 4: Add per-tab state and bounded recent recording**

Add to `ConnectionTabViewModel`:

```csharp
[ObservableProperty] private NamespaceWorkspaceMode _workspaceMode = NamespaceWorkspaceMode.Overview;
[ObservableProperty] private NamespaceOverviewSection _overviewSection = NamespaceOverviewSection.Home;

public ObservableCollection<RecentEntityDestination> RecentDestinations { get; } = [];

public void RecordRecentDestination(NamespaceNavigationRequest request)
{
    var existing = RecentDestinations.FirstOrDefault(item => item.Request == request);
    if (existing is not null)
    {
        RecentDestinations.Remove(existing);
    }

    RecentDestinations.Insert(0, new RecentEntityDestination(request, DateTimeOffset.UtcNow));
    while (RecentDestinations.Count > 5)
    {
        RecentDestinations.RemoveAt(RecentDestinations.Count - 1);
    }
}
```

Reset `WorkspaceMode` and `OverviewSection` after successful connection and on disconnect.

- [ ] **Step 5: Run focused tests and commit**

Run command from Step 2. Expected: PASS.

```bash
git add BusLane/Models/Dashboard/NamespaceWorkspaceNavigation.cs BusLane/ViewModels/Core/ConnectionTabViewModel.cs BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
git commit -m "feat: add namespace workspace navigation state"
```

---

### Task 2: Make Overview normal namespace content

**Files:**
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs`
- Modify: `BusLane/Views/MainWindow.axaml`
- Modify: `BusLane/Views/Controls/NavigationSidebar.axaml`
- Test: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Test: `BusLane.Tests/ViewModels/Core/FeaturePanelsViewModelTests.cs`
- Test: `BusLane.Tests/Views/MainWindowViewTests.cs`
- Test: `BusLane.Tests/Views/NavigationSidebarTests.cs`

**Interfaces:**
- Consumes: Task 1 `NamespaceWorkspaceMode` and existing dashboard activation methods.
- Produces: `IsNamespaceOverviewVisible`, `IsAzureEntityWorkspaceVisible`, `IsConnectionStringEntityWorkspaceVisible`, `OpenOverviewCommand`, and `BackToOverviewCommand`. Removes `FeaturePanelsViewModel.ShowCharts`, `OpenCharts`, and `CloseCharts`.

- [ ] **Step 1: Write failing workspace visibility tests**

Add tests:

```csharp
[Fact]
public void OpenOverview_UsesActiveNamespaceWorkspaceInsteadOfFeatureOverlay()
{
    using var sut = CreateSut(new TestPreferencesService());
    var tab = CreateTab("tab-1", new TestPreferencesService());
    tab.WorkspaceMode = NamespaceWorkspaceMode.Entity;
    sut.ConnectionTabs.Add(tab);
    sut.ActiveTab = tab;

    sut.OpenOverviewCommand.Execute(null);

    tab.WorkspaceMode.Should().Be(NamespaceWorkspaceMode.Overview);
    sut.IsNamespaceOverviewVisible.Should().BeTrue();
}
```

Extend `MainWindowViewTests` to parse `MainWindow.axaml` and assert `NamespaceDashboardView` binds `IsNamespaceOverviewVisible`, while no dashboard element binds `FeaturePanels.ShowCharts`.

- [ ] **Step 2: Run tests and verify red**

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~FeaturePanelsViewModelTests|FullyQualifiedName~MainWindowViewTests|FullyQualifiedName~NavigationSidebarTests"
```

Expected: failures for missing commands/properties and existing overlay markup.

- [ ] **Step 3: Add computed workspace visibility and lifecycle**

Add to `MainWindowViewModel`:

```csharp
public bool IsNamespaceOverviewVisible =>
    ActiveTab?.IsConnected == true && ActiveTab.WorkspaceMode == NamespaceWorkspaceMode.Overview;

public bool IsAzureEntityWorkspaceVisible =>
    IsActiveTabAzureMode && ActiveTab?.WorkspaceMode == NamespaceWorkspaceMode.Entity;

public bool IsConnectionStringEntityWorkspaceVisible =>
    IsActiveTabConnectionStringMode && ActiveTab?.WorkspaceMode == NamespaceWorkspaceMode.Entity;

[RelayCommand]
private void OpenOverview()
{
    if (ActiveTab is null) return;
    ActiveTab.WorkspaceMode = NamespaceWorkspaceMode.Overview;
    NamespaceDashboard.Activate();
    NotifyActiveTabDependentProperties();
}

[RelayCommand]
private void BackToOverview() => OpenOverview();
```

When active tab changes, activate dashboard only when new tab is connected and in Overview mode; otherwise deactivate. Include the three new computed properties in `NotifyActiveTabDependentProperties` and respond to `WorkspaceMode` property changes.

- [ ] **Step 4: Move dashboard XAML and remove feature-panel ownership**

In `MainWindow.axaml`, add normal content:

```xml
<controls:NamespaceDashboardView Grid.Row="0"
                                 Grid.RowSpan="2"
                                 IsVisible="{Binding IsNamespaceOverviewVisible}"
                                 DataContext="{Binding NamespaceDashboard}"/>
```

Bind Azure and connection-string entity workspaces to the new entity visibility properties. Delete chart/dashboard overlay border. Remove dashboard visibility and methods from `FeaturePanelsViewModel`, plus corresponding tests and constructor-only custom dashboard coupling if no remaining caller needs it. Relabel sidebar entry from `Dashboard` to `Overview` and bind `OpenOverviewCommand`.

- [ ] **Step 5: Run focused tests and commit**

Run Step 2 command. Expected: PASS.

```bash
git add BusLane/ViewModels/MainWindowViewModel.cs BusLane/ViewModels/Core/FeaturePanelsViewModel.cs BusLane/Views/MainWindow.axaml BusLane/Views/Controls/NavigationSidebar.axaml BusLane.Tests/ViewModels/MainWindowViewModelTests.cs BusLane.Tests/ViewModels/Core/FeaturePanelsViewModelTests.cs BusLane.Tests/Views/MainWindowViewTests.cs BusLane.Tests/Views/NavigationSidebarTests.cs
git commit -m "feat: make dashboard namespace overview content"
```

---

### Task 3: Typed visible navigation and breadcrumb

**Files:**
- Modify: `BusLane/ViewModels/Dashboard/NamespaceInboxViewModel.cs`
- Modify: `BusLane/ViewModels/Dashboard/NamespaceInboxItemViewModel.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Create: `BusLane/Views/Controls/NamespaceWorkspaceBreadcrumb.axaml`
- Create: `BusLane/Views/Controls/NamespaceWorkspaceBreadcrumb.axaml.cs`
- Modify: `BusLane/Views/MainWindow.axaml`
- Test: `BusLane.Tests/ViewModels/Dashboard/NamespaceInboxViewModelTests.cs`
- Test: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Test: `BusLane.Tests/Views/MainWindowViewTests.cs`

**Interfaces:**
- Consumes: `NamespaceNavigationRequest`, `EntityWorkspaceView`, `ConnectionTabViewModel.RecordRecentDestination`.
- Produces: `NamespaceInboxViewModel.UpdateNavigation(Action<NamespaceNavigationRequest>)`, `NavigateToNamespaceDestinationAsync`, breadcrumb label properties, and visible destination loading.

- [ ] **Step 1: Write failing typed-intent and ordering tests**

Replace callback assertions with:

```csharp
[Fact]
public void OpenDeadLetter_EmitsTypedDestination()
{
    NamespaceNavigationRequest? request = null;
    var sut = CreateInboxWithQueue(open: value => request = value);

    sut.Items.Single().OpenDeadLetterCommand.Execute(null);

    request.Should().Be(new NamespaceNavigationRequest(
        EntityType.Queue,
        "orders",
        null,
        EntityWorkspaceView.DeadLetters));
}
```

Add a `MainWindowViewModelTests` case with a blocked `PeekMessagesAsync` task. Assert `ActiveTab.WorkspaceMode` becomes Entity before completing the network task, and `SelectedMessageTabIndex` is `1` for DLQ. Add a second case that starts navigation to A, then B, completes A last, and proves A cannot overwrite B's visible destination or status.

- [ ] **Step 2: Run focused tests and verify red**

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~NamespaceInboxViewModelTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~MainWindowViewTests"
```

- [ ] **Step 3: Replace three callbacks with typed navigation**

Change inbox construction to one `Action<NamespaceNavigationRequest>`. Commands create requests:

```csharp
private NamespaceNavigationRequest CreateRequest(EntityWorkspaceView view) =>
    new(Item.EntityType, Item.EntityName, Item.TopicName, view);
```

`OpenMessages` emits `ActiveMessages`, `OpenDeadLetter` emits `DeadLetters`, and `OpenSessionInspector` emits `Sessions`.

- [ ] **Step 4: Centralize destination navigation**

Implement:

```csharp
private async Task NavigateToNamespaceDestinationAsync(NamespaceNavigationRequest request)
{
    var tab = ActiveTab;
    if (tab is null) return;

    tab.WorkspaceMode = NamespaceWorkspaceMode.Entity;
    NotifyActiveTabDependentProperties();
    NamespaceDashboard.Deactivate();

    await SelectRequestedEntityAsync(request, tab);
    tab.RecordRecentDestination(request);
}
```

`SelectRequestedEntityAsync` resolves queues case-insensitively, resolves subscriptions using topic plus subscription name, calls existing selection logic, and maps destination to `SelectedMessageTabIndex` 0, 1, or 2. Topic requests call existing `SelectTopicAsync` and stop after subscriptions load. Record recent only after successful resolution/load. Keep stale entity error in visible entity workspace.

Increment a coordinator navigation generation before each request and check it after every awaited selection/load boundary. Reuse existing message/session cancellation where available; generation checks prevent an older completion from restoring stale selection, breadcrumb, recent history, or status.

- [ ] **Step 5: Add breadcrumb**

Expose `WorkspaceBreadcrumb` from current navigation and view. Add `NamespaceWorkspaceBreadcrumb` above both entity workspace variants. Bind Overview button to `BackToOverviewCommand`. Full subscription path must include topic and subscription.

- [ ] **Step 6: Run focused tests and commit**

Run Step 2 command. Expected: PASS.

```bash
git add BusLane/ViewModels/Dashboard/NamespaceInboxViewModel.cs BusLane/ViewModels/Dashboard/NamespaceInboxItemViewModel.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane/Views/Controls/NamespaceWorkspaceBreadcrumb.axaml BusLane/Views/Controls/NamespaceWorkspaceBreadcrumb.axaml.cs BusLane/Views/MainWindow.axaml BusLane.Tests/ViewModels/Dashboard/NamespaceInboxViewModelTests.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs BusLane.Tests/Views/MainWindowViewTests.cs
git commit -m "feat: route overview actions into visible workspace"
```

---

### Task 4: Actionable priority and reviewed suppression

**Files:**
- Modify: `BusLane/ViewModels/Dashboard/NamespaceInboxViewModel.cs`
- Modify: `BusLane/ViewModels/Dashboard/NamespaceInboxItemViewModel.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane/Views/Controls/NamespaceInboxView.axaml`
- Test: `BusLane.Tests/ViewModels/Dashboard/NamespaceInboxViewModelTests.cs`
- Test: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Test: `BusLane.Tests/Views/NamespaceDashboardViewTests.cs`

**Interfaces:**
- Consumes: ranked `NamespaceInboxItem` and `INamespaceInboxReviewStore`.
- Produces: `PriorityItems`, `AllIssues`, `NeedsActionCount`, `IsReviewed`, immediate review removal, max priority count of eight, and secondary Pin/Unpin and Copy Name actions.

- [ ] **Step 1: Write failing projection tests**

Add tests for healthy exclusion, cap, immediate review removal, unchanged suppression, worsening reappearance, and same-named subscriptions under different topics:

```csharp
[Fact]
public void Refresh_PriorityContainsOnlyTopEightActionableUnreviewedItems()
{
    _scoringService.Items = Enumerable.Range(0, 10)
        .Select(index => CreateInboxItem($"queue-{index}", EntityType.Queue, 10, index))
        .Append(CreateInboxItem("healthy", EntityType.Queue, 0, 0) with { Score = 0, Reasons = [] })
        .ToList();

    var sut = CreateSut();
    sut.Refresh("namespace", [], [], []);

    sut.PriorityItems.Should().HaveCount(8);
    sut.PriorityItems.Should().NotContain(item => item.EntityName == "healthy");
    sut.AllIssues.Should().HaveCount(10);
}

[Fact]
public void MarkReviewed_RemovesItemUntilCountsIncrease()
{
    var sut = CreateSutWithItem(active: 10, dead: 3);
    sut.PriorityItems.Single().MarkReviewedCommand.Execute(null);
    sut.PriorityItems.Should().BeEmpty();

    RefreshSameCounts(sut, active: 10, dead: 3);
    sut.PriorityItems.Should().BeEmpty();

    RefreshSameCounts(sut, active: 10, dead: 4);
    sut.PriorityItems.Should().ContainSingle();
}

[Fact]
public void ReviewIdentity_DistinguishesSameSubscriptionNameAcrossTopics()
{
    var sut = CreateSutWithSubscriptions(
        ("payments", "processor"),
        ("refunds", "processor"));

    sut.PriorityItems.Single(item => item.TopicName == "payments")
        .MarkReviewedCommand.Execute(null);

    sut.PriorityItems.Should().ContainSingle(item => item.TopicName == "refunds");
}
```

- [ ] **Step 2: Run focused tests and verify red**

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~NamespaceInboxViewModelTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~NamespaceDashboardViewTests"
```

- [ ] **Step 3: Implement projections and suppression**

Keep latest ranked snapshot and rebuild collections through one method:

```csharp
private const int MaxPriorityItems = 8;
private IReadOnlyList<NamespaceInboxItem> _latestRankedItems = [];

private static bool IsActionable(NamespaceInboxItem item) =>
    item.Score > 0 && item.Reasons.Count > 0;

private static bool HasWorsened(NamespaceInboxItem item, NamespaceInboxReviewState review) =>
    item.ActiveMessageCount > review.ActiveMessageCount ||
    item.DeadLetterCount > review.DeadLetterCount ||
    item.ScheduledCount > review.ScheduledCount ||
    item.ActiveAlertCount > review.ActiveAlertCount;
```

Build `AllIssues` from actionable items. Build `PriorityItems` from actionable items with no review or `HasWorsened`, ordered by score and capped at eight. Mark Reviewed saves state then calls projection rebuild immediately. Raise `NeedsActionCount`, `HasItems`, and empty-state notifications.

- [ ] **Step 4: Make primary action permanently visible**

Remove opacity/hit-test hiding styles. Choose primary action from reason/state: DLQ when dead letters are present, Sessions when session-enabled backlog is the actionable condition, otherwise Messages. Put Mark Reviewed, Pin/Unpin, and Copy Name in an always-reachable secondary menu. Main window delegates Pin/Unpin to current `NavigationState.TogglePin` and Copy Name to existing clipboard behavior; subscription copy text is full `topic/subscription` path. Add command tests and markup assertions proving no critical action depends on `:pointerover`.

- [ ] **Step 5: Run focused tests and commit**

Run Step 2 command. Expected: PASS.

```bash
git add BusLane/ViewModels/Dashboard/NamespaceInboxViewModel.cs BusLane/ViewModels/Dashboard/NamespaceInboxItemViewModel.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane/Views/Controls/NamespaceInboxView.axaml BusLane.Tests/ViewModels/Dashboard/NamespaceInboxViewModelTests.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs BusLane.Tests/Views/NamespaceDashboardViewTests.cs
git commit -m "feat: make namespace inbox actionable"
```

---

### Task 5: Entity search, pinned, and recent quick access

**Files:**
- Create: `BusLane/Models/Dashboard/NamespaceEntitySearchResult.cs`
- Create: `BusLane/ViewModels/Dashboard/NamespaceEntitySearchViewModel.cs`
- Create: `BusLane/Views/Controls/NamespaceEntitySearchView.axaml`
- Create: `BusLane/Views/Controls/NamespaceEntitySearchView.axaml.cs`
- Modify: `BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane/Views/MainWindow.axaml.cs`
- Test: `BusLane.Tests/ViewModels/Dashboard/NamespaceEntitySearchViewModelTests.cs`
- Test: `BusLane.Tests/ViewModels/Dashboard/NamespaceDashboardViewModelTests.cs`
- Test: `BusLane.Tests/Views/MainWindowViewTests.cs`

**Interfaces:**
- Consumes: queue/topic inventories, dashboard subscription snapshot, current pins, per-tab recent destinations, and typed navigation callback.
- Produces: `NamespaceEntitySearchViewModel.Query`, `Results`, `SelectedResult`, `OpenSelectedCommand`, `NamespaceDashboardViewModel.PinnedDestinations`, and `RecentDestinations`.

- [ ] **Step 1: Write failing search tests**

Cover grouping, path display, fuzzy subsequence matching, default destinations, result cap, Up/Down selection, Enter, and Escape behavior:

```csharp
[Theory]
[InlineData("ord", "orders")]
[InlineData("oreu", "orders-eu")]
public void Query_MatchesContainsAndSubsequence(string query, string expected)
{
    var sut = CreateSut();
    sut.UpdateInventory([Queue("orders"), Queue("orders-eu")], [], []);
    sut.Query = query;

    sut.Results.Should().Contain(item => item.EntityName == expected);
}

[Fact]
public void OpenSelected_SubscriptionUsesFullPathAndActiveDestination()
{
    NamespaceNavigationRequest? opened = null;
    var sut = new NamespaceEntitySearchViewModel(request => opened = request);
    sut.UpdateInventory([], [], [Subscription("payments", "fraud-indexer")]);
    sut.Query = "fraud";
    sut.SelectedResult = sut.Results.Single();

    sut.OpenSelectedCommand.Execute(null);

    opened.Should().Be(new NamespaceNavigationRequest(
        EntityType.Subscription,
        "payments/fraud-indexer",
        "payments",
        EntityWorkspaceView.ActiveMessages));
}
```

- [ ] **Step 2: Run search tests and verify red**

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~NamespaceEntitySearchViewModelTests|FullyQualifiedName~NamespaceDashboardViewModelTests"
```

- [ ] **Step 3: Implement deterministic in-memory matching**

`NamespaceEntitySearchResult` stores `DisplayPath`, `TypeLabel`, and request. Rank exact prefix before substring before subsequence; then sort by display path. Empty query returns no dropdown results. Cap dropdown at 30.

```csharp
private static int GetMatchRank(string candidate, string query)
{
    if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0;
    if (candidate.Contains(query, StringComparison.OrdinalIgnoreCase)) return 1;
    return IsSubsequence(candidate, query) ? 2 : int.MaxValue;
}
```

Use Active Messages for queues/subscriptions and Topic Subscriptions for topics.

- [ ] **Step 4: Feed complete active-namespace inventory**

Add `NamespaceDashboardViewModel.SetNavigationContext(...)` receiving current queues, topics, pins, and recents. Merge dashboard refresh subscriptions into search inventory on `EntitiesUpdated`. Rebuild pinned links from `PinnedEntity` and recent links from active tab collection. Main window calls this on active tab change, pin change, entity refresh, and successful navigation.

- [ ] **Step 5: Build virtualized search UI**

Use `ListBox` with `VirtualizingStackPanel`, `SelectedItem`, and command on Enter. Show queue/topic/subscription type and full display path. Give TextBox automation name `Search queues, topics, and subscriptions` and tooltip `/`. In control key handling, Up/Down changes selection, Enter opens, and Escape first clears results then releases search focus. Route `/` from `MainWindow` to overview search only when focus is not inside an editable control. Add markup/key-routing tests so message search shortcut behavior remains intact in Entity mode.

- [ ] **Step 6: Run focused tests and commit**

Run Step 2 command. Expected: PASS.

```bash
git add BusLane/Models/Dashboard/NamespaceEntitySearchResult.cs BusLane/ViewModels/Dashboard/NamespaceEntitySearchViewModel.cs BusLane/Views/Controls/NamespaceEntitySearchView.axaml BusLane/Views/Controls/NamespaceEntitySearchView.axaml.cs BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane/Views/MainWindow.axaml.cs BusLane.Tests/ViewModels/Dashboard/NamespaceEntitySearchViewModelTests.cs BusLane.Tests/ViewModels/Dashboard/NamespaceDashboardViewModelTests.cs BusLane.Tests/Views/MainWindowViewTests.cs
git commit -m "feat: add namespace overview entity search"
```

---

### Task 6: Triage Home, Issues, and Analytics layout

**Files:**
- Modify: `BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs`
- Modify: `BusLane/Views/Controls/NamespaceDashboardView.axaml`
- Create: `BusLane/Views/Controls/NamespaceIssuesView.axaml`
- Create: `BusLane/Views/Controls/NamespaceIssuesView.axaml.cs`
- Modify: `BusLane/Styles/AppStyles.axaml`
- Test: `BusLane.Tests/ViewModels/Dashboard/NamespaceDashboardViewModelTests.cs`
- Test: `BusLane.Tests/Views/NamespaceDashboardViewTests.cs`

**Interfaces:**
- Consumes: Task 4 inbox projections, Task 5 search/quick access, existing metric cards and charts.
- Produces: overview section commands, health-strip drill-down commands, issue filters, and approved visual hierarchy.

- [ ] **Step 1: Write failing section and markup contract tests**

Add view-model tests that `ShowIssuesCommand` selects Issues, `ShowAnalyticsCommand` selects Analytics, and `ShowHomeCommand` returns Home. Add health-strip command tests: Needs Action opens all issues, Total DLQ opens issues filtered to dead-letter problems, and Active Messages opens inventory sorted by active count. Replace old four-card/chart-order markup assertions with contracts proving search precedes priority, home renders only three health values, charts bind only inside Analytics, and issue list uses `VirtualizingStackPanel`.

- [ ] **Step 2: Run tests and verify red**

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~NamespaceDashboardViewModelTests|FullyQualifiedName~NamespaceDashboardViewTests"
```

- [ ] **Step 3: Add overview section state**

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsHomeSelected))]
[NotifyPropertyChangedFor(nameof(IsIssuesSelected))]
[NotifyPropertyChangedFor(nameof(IsAnalyticsSelected))]
private NamespaceOverviewSection _selectedSection = NamespaceOverviewSection.Home;

public bool IsHomeSelected => SelectedSection == NamespaceOverviewSection.Home;
public bool IsIssuesSelected => SelectedSection == NamespaceOverviewSection.Issues;
public bool IsAnalyticsSelected => SelectedSection == NamespaceOverviewSection.Analytics;

[RelayCommand] private void ShowHome() => SelectedSection = NamespaceOverviewSection.Home;
[RelayCommand] private void ShowIssues() => SelectedSection = NamespaceOverviewSection.Issues;
[RelayCommand] private void ShowAnalytics() => SelectedSection = NamespaceOverviewSection.Analytics;
```

Synchronize selected section with active tab `OverviewSection` in main-window context updates.

- [ ] **Step 4: Recompose approved Home**

Order:

1. Namespace title, refresh timestamp/status, auto-refresh controls.
2. `NamespaceEntitySearchView`.
3. Three compact values: Needs Action, Total DLQ, Active Messages.
4. Main two-column area: Priority Work (dominant) and Continue Work (Pinned, Recent, Analytics link).

Remove Top Entities and full chart grid from Home. Keep scheduled and size data inside Analytics. Use existing semantic tokens and radius scale; do not add a new palette.

- [ ] **Step 5: Add Issues and Analytics sections**

Issues uses virtualized `AllIssues` plus severity, entity-type, and reviewed-state filters. Active Messages drill-down reuses this virtualized inventory surface sorted by active count instead of introducing another unbounded list. Analytics contains existing four charts and time controls. Both expose Home through section navigation; opening an entity preserves previous overview section for Back behavior.

- [ ] **Step 6: Run focused tests and commit**

Run Step 2 command. Expected: PASS.

```bash
git add BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs BusLane/Views/Controls/NamespaceDashboardView.axaml BusLane/Views/Controls/NamespaceIssuesView.axaml BusLane/Views/Controls/NamespaceIssuesView.axaml.cs BusLane/Styles/AppStyles.axaml BusLane.Tests/ViewModels/Dashboard/NamespaceDashboardViewModelTests.cs BusLane.Tests/Views/NamespaceDashboardViewTests.cs
git commit -m "feat: build namespace triage home"
```

---

### Task 7: Refresh resilience, loading states, and accessibility

**Files:**
- Modify: `BusLane/Models/Dashboard/NamespaceEntitySnapshot.cs`
- Modify: `BusLane/Services/Dashboard/IDashboardRefreshService.cs`
- Modify: `BusLane/Services/Dashboard/DashboardRefreshService.cs`
- Modify: `BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs`
- Modify: `BusLane/Views/Controls/NamespaceDashboardView.axaml`
- Modify: `BusLane/Views/Controls/NamespaceIssuesView.axaml`
- Modify: `BusLane/Views/Controls/NamespaceInboxView.axaml`
- Test: `BusLane.Tests/Services/Dashboard/DashboardRefreshServiceTests.cs`
- Test: `BusLane.Tests/ViewModels/Dashboard/NamespaceDashboardViewModelTests.cs`
- Test: `BusLane.Tests/Views/NamespaceDashboardViewTests.cs`

**Interfaces:**
- Consumes: existing refresh generation/cancellation and last valid dashboard presentation.
- Produces: `DashboardRefreshSection`, `DashboardRefreshFailure`, `RefreshFailed`, `IsInitialLoading`, `HasSnapshot`, `RefreshErrorMessage`, and stale-data timestamp behavior.

- [ ] **Step 1: Write failing resilience tests**

Add service test where topics fail but queues succeed. Expect queue summary/entity snapshot publication plus `RefreshFailed` for Topics. Add VM test where later refresh fails and existing metric/inbox data remains while error text becomes visible. Add a Retry test proving the failed section is requested without blanking successful sections. Add markup tests for initial skeleton, updating label, section retry, and error automation names.

- [ ] **Step 2: Run resilience tests and verify red**

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~DashboardRefreshServiceTests|FullyQualifiedName~NamespaceDashboardViewModelTests|FullyQualifiedName~NamespaceDashboardViewTests"
```

- [ ] **Step 3: Add scoped failure model**

Add alongside snapshot models:

```csharp
public enum DashboardRefreshSection { Queues, Topics, Subscriptions }

public sealed record DashboardRefreshFailure(
    DashboardRefreshSection Section,
    string Message,
    DateTimeOffset Timestamp);
```

Add `event EventHandler<DashboardRefreshFailure>? RefreshFailed` to refresh interface/service.

- [ ] **Step 4: Preserve successful refresh sections**

Load queues and topics independently. Publish failure for failed source and continue with successful source. When topics fail, retain cached subscriptions and mark summary partial. When queue load fails, retain cached queues. Never replace last cache with an empty failed result. Keep generation checks before every event publication.

- [ ] **Step 5: Add view-model presentation states**

`IsInitialLoading` is true only while no successful snapshot exists. Later refresh keeps `HasSnapshot` content visible and sets Updating. `RefreshFailed` sets contextual message without clearing cards, priority, search, or charts. Successful section refresh clears its error. `LastRefreshTime` updates only after a useful snapshot.

Expose a section Retry command carrying `DashboardRefreshSection`. Route it to the smallest supported refresh operation; when service cannot independently load Subscriptions, retry Topics plus Subscriptions while retaining queue data. Disable only that section's Retry while its request is active.

- [ ] **Step 6: Complete accessible states and virtualization**

Use `VirtualizingStackPanel` for search and full issues. Keep Priority capped and non-virtualized. Add full-path tooltips, `TextTrimming="CharacterEllipsis"`, descriptive `AutomationProperties.Name`, visible focus states, and section-shaped skeletons. Verify minimum-window layout does not horizontally scroll primary actions.

- [ ] **Step 7: Run focused tests and commit**

Run Step 2 command. Expected: PASS.

```bash
git add BusLane/Models/Dashboard/NamespaceEntitySnapshot.cs BusLane/Services/Dashboard/IDashboardRefreshService.cs BusLane/Services/Dashboard/DashboardRefreshService.cs BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs BusLane/Views/Controls/NamespaceDashboardView.axaml BusLane/Views/Controls/NamespaceIssuesView.axaml BusLane/Views/Controls/NamespaceInboxView.axaml BusLane.Tests/Services/Dashboard/DashboardRefreshServiceTests.cs BusLane.Tests/ViewModels/Dashboard/NamespaceDashboardViewModelTests.cs BusLane.Tests/Views/NamespaceDashboardViewTests.cs
git commit -m "feat: add resilient overview refresh states"
```

---

### Task 8: End-to-end regression and final verification

**Files:**
- Modify: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Modify: `BusLane.Tests/Views/MainWindowViewTests.cs`
- Modify: `BusLane.Tests/Views/NamespaceDashboardViewTests.cs`
- Modify only if failures require: files touched by Tasks 1-7.

**Interfaces:**
- Consumes: all prior tasks.
- Produces: full workflow regression evidence and final clean diff.

- [ ] **Step 1: Add cross-component regression cases**

Cover these exact flows:

```text
connect -> Overview visible
Overview Open DLQ -> Entity visible -> DLQ selected -> load completes
Overview Open Sessions -> Entity visible -> Sessions selected
Overview search topic -> Topic subscriptions visible, no message peek
Entity Back -> previous Overview section and query retained
switch two tabs -> each restores Overview or Entity mode
stale dashboard request -> visible error, no silent entity substitution
```

Use blocked `TaskCompletionSource` service calls where ordering matters. XAML contract tests must assert dashboard no longer appears in feature overlay stack.

- [ ] **Step 2: Run all focused operator-home tests**

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~ConnectionTabViewModelTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~NamespaceDashboardViewModelTests|FullyQualifiedName~NamespaceInboxViewModelTests|FullyQualifiedName~NamespaceEntitySearchViewModelTests|FullyQualifiedName~DashboardRefreshServiceTests|FullyQualifiedName~MainWindowViewTests|FullyQualifiedName~NamespaceDashboardViewTests|FullyQualifiedName~NavigationSidebarTests"
```

Expected: zero failures.

- [ ] **Step 3: Run build and full suite**

```bash
dotnet build --no-incremental
dotnet test --no-build
git diff --check
```

Expected: build exit 0, all non-skipped tests pass, diff check empty. Record any pre-existing warning separately; do not claim zero warnings unless output proves it.

- [ ] **Step 4: Manual operator smoke test**

Run application and verify with one namespace containing queues, topics, subscriptions, DLQ counts, and a session-enabled entity:

```bash
dotnet run --project BusLane/BusLane.csproj
```

Verify light and dark themes, 1000 by 600 minimum window, keyboard search, queue active/DLQ, subscription DLQ, sessions, topic subscriptions, Overview return, Issues, Analytics, and tab switching.

- [ ] **Step 5: Review and commit final regression adjustments**

```bash
git add BusLane BusLane.Tests
git commit -m "test: cover dashboard operator workflow"
```

Do not stage unrelated untracked files under `docs/superpowers/plans/2026-08-21-*` or `docs/superpowers/specs/2026-08-21-*`.
