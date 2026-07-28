# Correlation Explorer Live Filtering and Comparison Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add automatic catalog updates, catalog-wide structured filtering, and two-message comparison to the Correlation Explorer.

**Architecture:** Extend the bounded message catalog with post-lock change notifications, then let a disposable explorer ViewModel coalesce those notifications before rebuilding filtered snapshots on the UI thread. Keep filtering and comparison deterministic and stateless through dedicated models/services so their behavior is easy to test independently from Avalonia.

**Tech Stack:** .NET 9, C#, Avalonia, CommunityToolkit.Mvvm, xUnit, FluentAssertions, NSubstitute.

---

### Task 1: Stable message identity and catalog change notifications

**Files:**
- Modify: `BusLane/Models/CorrelationMessage.cs`
- Modify: `BusLane/Services/ServiceBus/CorrelationMessageCatalog.cs`
- Modify: `BusLane.Tests/Services/ServiceBus/CorrelationMessageCatalogTests.cs`

**Step 1: Write failing identity and notification tests**

Add tests proving:

```csharp
[Fact]
public void Add_AfterMutation_RaisesChangedWithAffectedGroup()
{
    var sut = new CorrelationMessageCatalog();
    CorrelationCatalogChangedEventArgs? observed = null;
    sut.Changed += (_, args) =>
    {
        observed = args;
        sut.GetGroups().Should().ContainSingle();
    };

    sut.Add(CreateMessage("message-1", correlationId: "corr-1"));

    observed.Should().NotBeNull();
    observed!.AffectedGroupKeys.Should().ContainSingle("corr:corr-1");
    observed.ChangeKind.Should().Be(CorrelationCatalogChangeKind.Added);
}
```

Cover:

- stable `CorrelationMessageIdentity.From(message)` values;
- add and replacement notifications;
- eviction including both added and evicted group keys;
- a single coalesced notification from `AddRange`;
- clear notification;
- no notification when a range contains no groupable messages;
- an event subscriber calling `GetGroups()` successfully, proving notification occurs outside the lock.

**Step 2: Run tests to verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationMessageCatalogTests"
```

Expected: FAIL because the identity and change event do not exist.

**Step 3: Add the notification contract**

Add:

```csharp
public readonly record struct CorrelationMessageIdentity(
    string NamespaceName,
    string EntityName,
    long SequenceNumber,
    string MessageId)
{
    public static CorrelationMessageIdentity From(CorrelationMessage message) =>
        new(message.NamespaceName, message.EntityName, message.SequenceNumber, message.MessageId);
}

public enum CorrelationCatalogChangeKind
{
    Added,
    Replaced,
    Evicted,
    RangeAdded,
    Cleared
}

public sealed class CorrelationCatalogChangedEventArgs(
    CorrelationCatalogChangeKind changeKind,
    IReadOnlySet<string> affectedGroupKeys) : EventArgs
{
    public CorrelationCatalogChangeKind ChangeKind { get; } = changeKind;
    public IReadOnlySet<string> AffectedGroupKeys { get; } = affectedGroupKeys;
}
```

Extend the interface:

```csharp
event EventHandler<CorrelationCatalogChangedEventArgs>? Changed;
```

Reuse `CorrelationMessageIdentity` for catalog deduplication. Capture mutation results under the lock, then invoke `Changed` after leaving the lock. Implement `AddRange` as one locked mutation and one notification instead of repeatedly calling `Add`.

**Step 4: Run focused tests to verify GREEN**

Run the Task 1 filtered command again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Models/CorrelationMessage.cs BusLane/Services/ServiceBus/CorrelationMessageCatalog.cs BusLane.Tests/Services/ServiceBus/CorrelationMessageCatalogTests.cs
rtk git commit -m "feat: publish correlation catalog changes"
```

### Task 2: Deterministic message comparison

**Files:**
- Create: `BusLane/Models/CorrelationMessageComparison.cs`
- Create: `BusLane/Services/ServiceBus/CorrelationMessageComparisonService.cs`
- Create: `BusLane.Tests/Services/ServiceBus/CorrelationMessageComparisonServiceTests.cs`

**Step 1: Write failing comparison tests**

Test:

- identical JSON with different whitespace produces no body change;
- changed JSON produces a normalized before/after result;
- malformed JSON falls back to plain text;
- standard metadata changes include correlation, session, message ID, subject, routing, content type, entity, namespace, environment, and source;
- application properties are classified as added, removed, modified, or unchanged;
- enqueue-time delta is calculated from message B minus message A;
- source messages and property dictionaries are never mutated.

Representative assertion:

```csharp
var result = sut.Compare(first, second);

result.Body.Kind.Should().Be(MessageBodyComparisonKind.Json);
result.Body.IsChanged.Should().BeTrue();
result.PropertyChanges.Should().Contain(change =>
    change.Key == "tenant" &&
    change.Kind == MessagePropertyChangeKind.Modified);
result.EnqueueTimeDelta.Should().Be(TimeSpan.FromSeconds(5));
```

**Step 2: Run tests to verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationMessageComparisonServiceTests"
```

Expected: FAIL because comparison types do not exist.

**Step 3: Implement comparison models and service**

Create immutable records/enums for:

```csharp
MessageComparison
MessageFieldChange
MessagePropertyChange
MessagePropertyChangeKind
MessageBodyComparison
MessageBodyComparisonKind
```

Define:

```csharp
public interface ICorrelationMessageComparisonService
{
    MessageComparison Compare(CorrelationMessage first, CorrelationMessage second);
}
```

Use `JsonDocument.Parse` and `JsonSerializer.Serialize` with indentation to normalize valid JSON. Compare non-JSON bodies with ordinal string equality. Convert property values to display strings only in the comparison result; preserve the original typed values.

**Step 4: Run comparison tests to verify GREEN**

Run the Task 2 filtered command again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Models/CorrelationMessageComparison.cs BusLane/Services/ServiceBus/CorrelationMessageComparisonService.cs BusLane.Tests/Services/ServiceBus/CorrelationMessageComparisonServiceTests.cs
rtk git commit -m "feat: compare correlated messages"
```

### Task 3: Catalog-wide structured filtering

**Files:**
- Create: `BusLane/Models/CorrelationExplorerFilter.cs`
- Create: `BusLane/Services/ServiceBus/CorrelationMessageFilter.cs`
- Create: `BusLane.Tests/Services/ServiceBus/CorrelationMessageFilterTests.cs`
- Modify: `BusLane/ViewModels/CorrelationExplorerViewModel.cs`
- Modify: `BusLane.Tests/ViewModels/CorrelationExplorerViewModelTests.cs`

**Step 1: Write failing filter tests**

Test the stateless filter for:

- empty filter matching every groupable message;
- free text across group ID, message ID, entity, namespace, body, and application-property keys/values;
- inclusive start/end times;
- namespace and entity;
- environment;
- source;
- explicit correlation/session identifier;
- property key with optional property value;
- combined criteria using AND semantics.

Add ViewModel tests proving groups remain visible only when they contain matching messages and their timelines contain only matching messages.

**Step 2: Run tests to verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationMessageFilterTests|FullyQualifiedName~CorrelationExplorerViewModelTests"
```

Expected: FAIL because structured filter state and matching do not exist.

**Step 3: Implement the filter model and matcher**

Create an immutable `CorrelationExplorerFilter` record with nullable structured criteria and a static `Empty` value.

Create:

```csharp
public interface ICorrelationMessageFilter
{
    bool Matches(CorrelationMessage message, CorrelationExplorerFilter filter);
}
```

Use ordinal-ignore-case matching for user-entered text and exact enum matching. Treat blank strings as absent criteria.

Extend `CorrelationExplorerViewModel` with observable editor fields:

```csharp
FilterText
FilterFromText
FilterToText
FilterNamespace
FilterEntity
FilterEnvironment
FilterSource
FilterIdentifier
FilterPropertyKey
FilterPropertyValue
ShowFilters
FilterValidationMessage
```

Build an immutable filter only when date/time text parses successfully. Preserve the current group and message through `CorrelationMessageIdentity`. Add `ApplyFiltersCommand`, `ClearFiltersCommand`, and `ToggleFiltersCommand`.

Do not filter the replay audit history in this task.

**Step 4: Run focused tests to verify GREEN**

Run the Task 3 filtered command again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Models/CorrelationExplorerFilter.cs BusLane/Services/ServiceBus/CorrelationMessageFilter.cs BusLane/ViewModels/CorrelationExplorerViewModel.cs BusLane.Tests/Services/ServiceBus/CorrelationMessageFilterTests.cs BusLane.Tests/ViewModels/CorrelationExplorerViewModelTests.cs
rtk git commit -m "feat: add structured correlation filters"
```

### Task 4: Live refresh, selection preservation, and disposal

**Files:**
- Create: `BusLane/Services/ServiceBus/CorrelationRefreshDelay.cs`
- Create: `BusLane.Tests/Services/ServiceBus/CorrelationRefreshDelayTests.cs`
- Modify: `BusLane/ViewModels/CorrelationExplorerViewModel.cs`
- Modify: `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs`
- Modify: `BusLane/Program.cs`
- Modify: `BusLane.Tests/ViewModels/CorrelationExplorerViewModelTests.cs`
- Modify: `BusLane.Tests/ViewModels/Core/FeaturePanelsViewModelTests.cs`

**Step 1: Write failing live-update tests**

Add a controllable test delay implementing:

```csharp
public interface ICorrelationRefreshDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct = default);
}
```

Test:

- a catalog event schedules a refresh;
- multiple events before delay completion produce one refresh;
- the selected group and message remain selected;
- a new message in the selected group increments `NewMessageCount`;
- acknowledging or selecting the newest message clears the count;
- filtered-out additions do not alter visible collections;
- `Dispose()` cancels pending work and unsubscribes;
- closing or replacing the feature panel disposes its explorer.

**Step 2: Run tests to verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationRefreshDelayTests|FullyQualifiedName~CorrelationExplorerViewModelTests|FullyQualifiedName~FeaturePanelsViewModelTests"
```

Expected: FAIL because live refresh and disposal are absent.

**Step 3: Implement debounced live refresh**

Add `CorrelationRefreshDelay` using `Task.Delay`. Register it as a singleton.

Make `CorrelationExplorerViewModel` implement `IDisposable`. Subscribe to `_catalog.Changed` in the constructor. On change:

1. cancel and dispose the previous debounce token source;
2. start one delayed refresh;
3. marshal `RefreshGroups` through `Dispatcher.UIThread` when an Avalonia application is active;
4. capture exceptions into `StatusMessage`;
5. ignore expected cancellation.

Use a small constant such as 100 milliseconds. Do not reload audit history for catalog-only changes.

Track the selected group’s last known message identities to calculate `NewMessageCount`. Add `AcknowledgeNewMessagesCommand`.

Update `FeaturePanelsViewModel` so `OpenCorrelationExplorer` disposes an existing explorer before replacing it, and `CloseCorrelationExplorer` disposes before clearing it.

**Step 4: Run focused tests to verify GREEN**

Run the Task 4 filtered command again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Services/ServiceBus/CorrelationRefreshDelay.cs BusLane/ViewModels/CorrelationExplorerViewModel.cs BusLane/ViewModels/Core/FeaturePanelsViewModel.cs BusLane/Program.cs BusLane.Tests/Services/ServiceBus/CorrelationRefreshDelayTests.cs BusLane.Tests/ViewModels/CorrelationExplorerViewModelTests.cs BusLane.Tests/ViewModels/Core/FeaturePanelsViewModelTests.cs
rtk git commit -m "feat: refresh correlation explorer live"
```

### Task 5: Comparison state and commands

**Files:**
- Modify: `BusLane/ViewModels/CorrelationExplorerViewModel.cs`
- Modify: `BusLane.Tests/ViewModels/CorrelationExplorerViewModelTests.cs`
- Modify: `BusLane/Program.cs`

**Step 1: Write failing ViewModel comparison tests**

Test:

- assigning selected messages to comparison A and B;
- comparison is produced only when both slots are populated;
- `CompareWithPrevious` uses chronological timeline order;
- the first message reports that no previous message exists;
- replacing either slot recomputes the comparison;
- filtering or eviction clears only a comparison slot whose identity no longer exists;
- clearing comparison resets both slots and the result.

**Step 2: Run tests to verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationExplorerViewModelTests"
```

Expected: FAIL because comparison state and commands are absent.

**Step 3: Add comparison state**

Inject `ICorrelationMessageComparisonService`. Expose:

```csharp
CorrelationMessage? ComparisonMessageA
CorrelationMessage? ComparisonMessageB
MessageComparison? Comparison
bool HasComparison
```

Add commands:

```csharp
SetComparisonA
SetComparisonB
CompareWithPrevious
ClearComparison
```

Recompute through one private method whenever either slot changes. Resolve identities against the current catalog snapshot after refresh so evicted messages are detected even when filters temporarily hide a message.

Register `ICorrelationMessageComparisonService` as a singleton in `Program.cs`.

**Step 4: Run ViewModel tests to verify GREEN**

Run the Task 5 filtered command again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/ViewModels/CorrelationExplorerViewModel.cs BusLane/Program.cs BusLane.Tests/ViewModels/CorrelationExplorerViewModelTests.cs
rtk git commit -m "feat: add explorer comparison workflow"
```

### Task 6: Filter and comparison UI

**Files:**
- Modify: `BusLane/Views/Controls/CorrelationExplorerView.axaml`
- Modify: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Write failing XAML structure tests**

Assert the view contains:

- toggle and clear-filter commands;
- all structured filter bindings;
- apply-filter validation feedback;
- a visible new-message indicator and acknowledge command;
- commands for comparison A, B, and previous;
- a Compare tab;
- bindings for body, metadata, property, timing, and source differences;
- a clear-comparison command;
- collapsible filter content that does not add another fixed-width main column.

**Step 2: Run tests to verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationExplorerViewTests"
```

Expected: FAIL because the new controls and bindings are absent.

**Step 3: Implement the UI**

Add a collapsible filter row below the explorer header. Use existing Fluent classes and compact controls.

Add comparison actions to timeline rows and the selected-message header. Add a Compare tab with:

- comparison message labels;
- enqueue-time delta;
- an item list of changed standard fields;
- added/removed/modified property rows;
- side-by-side normalized bodies where width permits;
- wrapped stacked bodies at narrow widths.

Keep replay actions unchanged.

**Step 4: Run UI tests and build**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationExplorerViewTests"
rtk dotnet build
```

Expected: tests pass and build completes with no new warnings.

**Step 5: Commit**

```bash
rtk git add BusLane/Views/Controls/CorrelationExplorerView.axaml BusLane.Tests/Views/CorrelationExplorerViewTests.cs
rtk git commit -m "feat: add live filter and comparison explorer UI"
```

### Task 7: Integration and regression verification

**Files:**
- Modify if required by composition tests: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Modify if required by shared XAML checks: `BusLane.Tests/Views/CodeEditorStyleTests.cs`

**Step 1: Add integration coverage**

Add or extend tests proving:

- production composition resolves the new filter, comparison, and refresh services;
- opening the explorer observes messages subsequently ingested through loaded and live-stream paths;
- closing the explorer leaves the shared catalog intact;
- reopening creates one subscriber and displays the current snapshot;
- replay remains available after filtering and comparison.

**Step 2: Run focused integration tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~FeaturePanelsViewModelTests|FullyQualifiedName~CorrelationExplorerViewModelTests"
```

Expected: PASS.

**Step 3: Run formatting and complete verification**

Run:

```bash
rtk dotnet build
rtk dotnet test --no-build
rtk proxy git diff --check
rtk git status --short
```

Expected:

- build completes with zero errors and no new warnings;
- the full suite passes;
- diff check is clean;
- only intended files are changed.

**Step 4: Update the architecture graph**

Run:

```bash
rtk graphify update .
rtk graphify query "CorrelationExplorerViewModel CorrelationMessageComparisonService CorrelationMessageFilter"
```

Verify the new filter, comparison, and event relationships are present. Keep generated graph artifacts out of the feature commit unless they are already tracked by the repository.

**Step 5: Review the complete change**

Use `superpowers:requesting-code-review`. Address validated findings with `superpowers:test-driven-development`, then rerun the complete verification commands.

**Step 6: Commit any final integration changes**

```bash
rtk git add BusLane BusLane.Tests
rtk git commit -m "test: verify live correlation investigation workflow"
```
