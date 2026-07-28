# Message Replay and Correlation Explorer Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add an in-session correlation timeline and a safe, audited single-message replay workflow for messages visible to BusLane.

**Architecture:** Normalize loaded and streamed messages into a shared bounded catalog, expose grouped timelines through a dedicated feature panel, and route replay through one service responsible for request validation, preview, rate limiting, send/schedule, and audit persistence. Existing Service Bus operations, environment tags, file dialogs, and secure local storage remain the integration boundaries.

**Tech Stack:** .NET 9, C#, Avalonia, CommunityToolkit.Mvvm, Azure.Messaging.ServiceBus abstractions, xUnit, FluentAssertions, NSubstitute.

---

### Task 1: Correlation models and bounded catalog

**Files:**
- Create: `BusLane/Models/CorrelationMessage.cs`
- Create: `BusLane/Services/ServiceBus/CorrelationMessageCatalog.cs`
- Create: `BusLane.Tests/Services/ServiceBus/CorrelationMessageCatalogTests.cs`

**Step 1: Write failing grouping tests**

Add tests that construct loaded and streamed normalized messages and assert:

```csharp
var groups = sut.GetGroups();

groups.Single(g => g.Key == "corr:corr-1")
    .Messages.Select(m => m.EnqueuedTime)
    .Should().BeInAscendingOrder();
```

Cover:

- `CorrelationId` as the primary key;
- `SessionId` fallback;
- omission when both identifiers are blank;
- deduplication by source namespace, entity, sequence number, and message ID;
- eviction of the oldest observation when capacity is exceeded.

**Step 2: Run tests to verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationMessageCatalogTests"
```

Expected: FAIL because the models and catalog do not exist.

**Step 3: Implement the minimal models**

Create:

```csharp
public enum CorrelationMessageSource { Loaded, LiveStream }

public sealed record CorrelationMessage(
    CorrelationMessageSource Source,
    string NamespaceName,
    ConnectionEnvironment Environment,
    string EntityName,
    string EntityType,
    string? TopicName,
    string? SubscriptionName,
    string MessageId,
    string? CorrelationId,
    string? SessionId,
    string? ContentType,
    string Body,
    DateTimeOffset EnqueuedTime,
    long SequenceNumber,
    IReadOnlyDictionary<string, object> Properties,
    string? Subject = null,
    string? To = null,
    string? ReplyTo = null,
    string? ReplyToSessionId = null,
    string? PartitionKey = null,
    TimeSpan? TimeToLive = null);

public sealed record CorrelationGroup(
    string Key,
    string DisplayId,
    bool UsesSessionFallback,
    IReadOnlyList<CorrelationMessage> Messages);
```

Create `ICorrelationMessageCatalog` with `Add`, `AddRange`, `GetGroups`, and `Clear`. Implement it with a per-instance lock and a capacity constructor defaulting to 2,000.

**Step 4: Run tests to verify GREEN**

Run the filtered test command again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Models/CorrelationMessage.cs BusLane/Services/ServiceBus/CorrelationMessageCatalog.cs BusLane.Tests/Services/ServiceBus/CorrelationMessageCatalogTests.cs
rtk git commit -m "feat: add bounded correlation message catalog"
```

### Task 2: Source normalization and ingestion

**Files:**
- Create: `BusLane/Services/ServiceBus/CorrelationMessageFactory.cs`
- Create: `BusLane.Tests/Services/ServiceBus/CorrelationMessageFactoryTests.cs`
- Modify: `BusLane/ViewModels/Core/MessageOperationsViewModel.cs`
- Modify: `BusLane/ViewModels/LiveStreamViewModel.cs`
- Modify: `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs`
- Modify: `BusLane/Program.cs`
- Modify: `BusLane.Tests/ViewModels/Core/MessageOperationsViewModelTests.cs`
- Modify: `BusLane.Tests/ViewModels/LiveStreamViewModelTests.cs`

**Step 1: Write failing factory and ingestion tests**

Test `CorrelationMessageFactory.FromLoaded` and `.FromLiveStream` for complete source/entity/metadata mapping and defensive copying of properties.

Add ViewModel tests proving:

```csharp
catalog.Received(1).AddRange(
    Arg.Is<IEnumerable<CorrelationMessage>>(items => items.All(i => i.Source == CorrelationMessageSource.Loaded)));
```

after a successful page display, and `catalog.Add(...)` after live-stream flush.

**Step 2: Run focused tests to verify RED**

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationMessageFactoryTests|FullyQualifiedName~MessageOperationsViewModelTests|FullyQualifiedName~LiveStreamViewModelTests"
```

Expected: FAIL because normalization and catalog dependencies are absent.

**Step 3: Implement normalization and ingestion**

Create a static factory that accepts explicit namespace/environment/entity context. Do not let it inspect connection strings.

Inject optional catalog/context callbacks into `MessageOperationsViewModel` and `LiveStreamViewModel` so existing tests and legacy construction remain compatible. Ingest only after messages are successfully materialized in the UI collection.

Pass catalog and context callbacks from both the legacy `MainWindowViewModel` components and each `ConnectionTabViewModel`. Register `ICorrelationMessageCatalog` as a singleton. Give `FeaturePanelsViewModel` the same singleton for the explorer.

**Step 4: Run focused and full tests**

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationMessageFactoryTests|FullyQualifiedName~MessageOperationsViewModelTests|FullyQualifiedName~LiveStreamViewModelTests"
rtk dotnet test
```

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane BusLane.Tests
rtk git commit -m "feat: capture loaded and streamed correlation messages"
```

### Task 3: Replay domain service and rate limiting

**Files:**
- Create: `BusLane/Models/MessageReplay.cs`
- Create: `BusLane/Services/ServiceBus/MessageReplayService.cs`
- Create: `BusLane.Tests/Services/ServiceBus/MessageReplayServiceTests.cs`

**Step 1: Write failing request, validation, and transport tests**

Cover:

- cloning all editable metadata and application properties;
- generating a new default message ID;
- retaining a deliberately supplied message ID;
- missing destination/body validation;
- empty or duplicate property-key validation;
- non-positive rate-limit validation;
- past scheduled-time validation;
- missing session ID for a session-required destination;
- general confirmation and production acknowledgment;
- `SendMessageAsync` for immediate replay;
- `ScheduleMessageAsync` for scheduled replay;
- throttling consecutive attempts through an injected delay abstraction.

Use an injected `TimeProvider` and `IReplayDelay` so tests never sleep.

**Step 2: Run tests to verify RED**

```bash
rtk dotnet test --filter "FullyQualifiedName~MessageReplayServiceTests"
```

Expected: FAIL because replay types and service do not exist.

**Step 3: Implement minimal replay types**

Add records for `ReplayDestination`, `ReplayRequest`, `ReplayFieldChange`, `ReplayPreview`, and `ReplayResult`. Include `ConnectionEnvironment`, `RequiresSession`, schedule, rate, and the two confirmation flags.

Define:

```csharp
public interface IMessageReplayService
{
    ReplayRequest CreateRequest(CorrelationMessage source, ReplayDestination destination);
    ReplayPreview Preview(ReplayRequest request);
    Task<ReplayResult> ReplayAsync(
        IServiceBusOperations operations,
        ReplayRequest request,
        CancellationToken ct = default);
}
```

The production service serializes access to the last-send timestamp, applies `TimeProvider`, invokes the delay abstraction, and then calls existing Service Bus operations.

**Step 4: Run tests to verify GREEN**

Run the filtered tests again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Models/MessageReplay.cs BusLane/Services/ServiceBus/MessageReplayService.cs BusLane.Tests/Services/ServiceBus/MessageReplayServiceTests.cs
rtk git commit -m "feat: add validated rate-limited replay service"
```

### Task 4: Secure replay audit and export

**Files:**
- Modify: `BusLane/Services/Infrastructure/AppPaths.cs`
- Create: `BusLane/Services/ServiceBus/ReplayAuditStore.cs`
- Create: `BusLane.Tests/Services/ServiceBus/ReplayAuditStoreTests.cs`
- Modify: `BusLane/Services/ServiceBus/MessageReplayService.cs`
- Modify: `BusLane.Tests/Services/ServiceBus/MessageReplayServiceTests.cs`

**Step 1: Write failing persistence tests**

Verify secure JSON round trips for validation failure, cancellation, attempt, success, and failure entries. Assert serialized text excludes `ConnectionString`, `SharedAccessKey`, and token-like values.

Add replay-service tests asserting the store is called for every outcome.

**Step 2: Run tests to verify RED**

```bash
rtk dotnet test --filter "FullyQualifiedName~ReplayAuditStoreTests|FullyQualifiedName~MessageReplayServiceTests"
```

Expected: FAIL because the audit store does not exist.

**Step 3: Implement the audit store**

Add `AppPaths.ReplayAudit`. Create `ReplayAuditEntry` and `IReplayAuditStore` with `LoadAsync` and `AddAsync`. Follow `ScheduledMessageStore`: per-instance `SemaphoreSlim`, `SafeJsonSerializer`, secure atomic writes, cancellation propagation, and an injectable path for tests.

Have `MessageReplayService` write validation failures, confirmation cancellations, attempts, successes, and transport failures. If audit persistence fails, preserve the replay result and set an `AuditWarning` field.

**Step 4: Run tests to verify GREEN**

Run the filtered tests again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Services/Infrastructure/AppPaths.cs BusLane/Services/ServiceBus/ReplayAuditStore.cs BusLane/Services/ServiceBus/MessageReplayService.cs BusLane.Tests/Services/ServiceBus
rtk git commit -m "feat: persist replay audit history securely"
```

### Task 5: Correlation Explorer and replay editor ViewModels

**Files:**
- Create: `BusLane/ViewModels/CorrelationExplorerViewModel.cs`
- Create: `BusLane/ViewModels/ReplayMessageViewModel.cs`
- Create: `BusLane.Tests/ViewModels/CorrelationExplorerViewModelTests.cs`
- Create: `BusLane.Tests/ViewModels/ReplayMessageViewModelTests.cs`
- Modify: `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs`
- Create: `BusLane.Tests/ViewModels/Core/FeaturePanelsViewModelTests.cs`

**Step 1: Write failing ViewModel tests**

Cover:

- refreshing and filtering groups;
- selecting a group and chronological message;
- opening a prefilled replay editor;
- destination queue/topic selection;
- editable standard and application properties;
- preview validation and changed fields;
- mandatory confirmation;
- production acknowledgment;
- visible success/failure/audit warning;
- replay-history loading and JSON export through `IFileDialogService`;
- opening and closing the explorer panel.

**Step 2: Run tests to verify RED**

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationExplorerViewModelTests|FullyQualifiedName~ReplayMessageViewModelTests|FullyQualifiedName~FeaturePanelsViewModelTests"
```

Expected: FAIL because the ViewModels do not exist.

**Step 3: Implement minimal ViewModels**

`CorrelationExplorerViewModel` owns group/timeline/history collections and creates `ReplayMessageViewModel` from the selected source. `ReplayMessageViewModel` converts editable `CustomProperty` rows into a `ReplayRequest`, requests a preview, and exposes a confirm command only when safety conditions pass.

Extend `FeaturePanelsViewModel` with `ShowCorrelationExplorer`, `CorrelationExplorerViewModel`, `OpenCorrelationExplorer`, and `CloseCorrelationExplorer`. Opening it closes Live Stream, Charts, and Alerts without clearing the shared catalog.

**Step 4: Run tests to verify GREEN**

Run the filtered tests again.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/ViewModels BusLane.Tests/ViewModels
rtk git commit -m "feat: add correlation explorer replay view models"
```

### Task 6: Explorer panel, replay dialog, and navigation

**Files:**
- Create: `BusLane/Views/Controls/CorrelationExplorerView.axaml`
- Create: `BusLane/Views/Controls/CorrelationExplorerView.axaml.cs`
- Create: `BusLane/Views/Dialogs/ReplayMessageDialog.axaml`
- Create: `BusLane/Views/Dialogs/ReplayMessageDialog.axaml.cs`
- Modify: `BusLane/Views/MainWindow.axaml`
- Modify: `BusLane/Views/Controls/NavigationSidebar.axaml`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Create: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`
- Create: `BusLane.Tests/Views/ReplayMessageDialogTests.cs`

**Step 1: Write failing structure and command tests**

Parse the XAML and assert:

- the sidebar exposes `OpenCorrelationExplorerCommand` in expanded and rail modes;
- MainWindow hosts the explorer panel;
- the timeline binds to chronological messages;
- detail tabs expose metadata, body, and application properties;
- replay dialog exposes destination, schedule, rate, preview, general confirmation, and production acknowledgment;
- send is disabled while validation or safety requirements are unmet.

**Step 2: Run tests to verify RED**

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationExplorerViewTests|FullyQualifiedName~ReplayMessageDialogTests"
```

Expected: FAIL because XAML files and bindings do not exist.

**Step 3: Implement the UI**

Follow existing Fluent 2 classes and panel layout. Add MainWindow commands that delegate to `FeaturePanels`. Use a three-column explorer layout where available width permits: groups, timeline, details/history. Show replay as an overlay dialog above the panel.

Display destination namespace and environment prominently in both the replay header and confirmation area. Use danger styling for Production.

**Step 4: Run focused UI tests and build**

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationExplorerViewTests|FullyQualifiedName~ReplayMessageDialogTests"
rtk dotnet build
```

Expected: PASS with no new warnings.

**Step 5: Commit**

```bash
rtk git add BusLane/Views BusLane/ViewModels/MainWindowViewModel.cs BusLane.Tests/Views
rtk git commit -m "feat: add correlation explorer and replay UI"
```

### Task 7: Dependency injection, integration, and regression verification

**Files:**
- Modify: `BusLane/Program.cs`
- Modify: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Modify: `BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs`
- Modify: `BusLane.Tests/Views/CodeEditorStyleTests.cs`

**Step 1: Write failing composition tests**

Add tests that construct `MainWindowViewModel`, open the explorer, ingest loaded and streamed messages, open replay for a selected timeline entry, and confirm that the active tab operations are used.

**Step 2: Run tests to verify RED**

```bash
rtk dotnet test --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~ConnectionTabViewModelTests"
```

Expected: FAIL until all service registrations and constructor wiring are complete.

**Step 3: Complete composition**

Register the catalog, audit store, replay delay, replay service, and explorer dependencies with appropriate singleton lifetimes. Ensure legacy optional constructors used by tests remain source-compatible only where that does not hide missing production dependencies.

Update shared dialog/style checks for the replay editor.

**Step 4: Run formatting, build, and full tests**

```bash
rtk dotnet build
rtk dotnet test
rtk git diff --check
```

Expected:

- build succeeds;
- all tests pass;
- no new compiler warnings;
- diff check is clean.

**Step 5: Update the project graph**

Because the change set is code-only, run:

```bash
rtk graphify update .
```

Verify the graph includes `CorrelationMessageCatalog`, `CorrelationExplorerViewModel`, and `MessageReplayService`.

**Step 6: Commit**

```bash
rtk git add BusLane BusLane.Tests graphify-out
rtk git commit -m "test: verify message replay correlation explorer"
```

### Task 8: Final review

**Files:**
- Review all files changed since `abfab45`.

**Step 1: Inspect the complete diff**

```bash
rtk git diff --stat abfab45..HEAD
rtk git diff --check abfab45..HEAD
rtk git status --short
```

Confirm every changed line maps to issue #155.

**Step 2: Run final verification**

```bash
rtk dotnet build
rtk dotnet test
```

Expected: build succeeds and the full suite passes.

**Step 3: Request code review**

Invoke `superpowers:requesting-code-review`, address any validated findings with TDD, and rerun final verification.
