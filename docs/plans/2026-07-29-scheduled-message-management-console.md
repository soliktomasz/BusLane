# Scheduled-Message Management Console Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a global console that securely indexes messages scheduled through BusLane and lets users search, filter, clone, cancel, reschedule, refresh, and locally resolve them.

**Architecture:** Replace the preview-only scheduled-message file with a backward-compatible, versioned store whose full payload is encrypted through `IEncryptionService`. Add a management service for connection resolution and broker actions, a dedicated feature-panel view model and Avalonia view for list/calendar presentation, and shared recording from both send and replay scheduling paths. Preserve a strict distinction between local derived state and broker-confirmed outcomes.

**Tech Stack:** .NET, C#, Avalonia UI, CommunityToolkit.Mvvm, Azure.Messaging.ServiceBus through `IServiceBusOperations`, xUnit, FluentAssertions, NSubstitute.

---

Implementation stays in the current checkout on `codex/issue-160-scheduled-message-console`; do not create a worktree.

### Task 1: Define the versioned scheduled-message domain

**Files:**
- Replace: `BusLane/Models/ScheduledMessageIndexEntry.cs`
- Modify: `BusLane.Tests/Models/ModelTests.cs`

**Step 1: Write failing model tests**

Add tests named:

```csharp
[Fact]
public void ScheduledMessageIndexEntry_NewRecord_ExposesLocalUpcomingState()

[Fact]
public void ScheduledMessageIndexEntry_LegacyRecord_DisablesPayloadActions()

[Fact]
public void ScheduledMessageIndexEntry_ConfirmedCancellation_IsBrokerConfirmed()
```

Assert the domain exposes:

- `ScheduledMessageRecordStatus` values `Indexed`, `Cancelled`, `Rescheduled`, `ActionFailed`, and `ResolvedLocally`;
- `ScheduledMessageConnectionKind` values `ConnectionString` and `AzureCredential`;
- a stable record ID and optional replacement record ID;
- connection ID/name, namespace endpoint, environment, and connection kind;
- entity/subscription identity, sequence number, scheduled/created/updated timestamps;
- searchable message metadata;
- `EncryptedPayload`;
- last broker action/error metadata;
- computed `HasPayload`, `IsBrokerConfirmed`, and `IsLegacyLimited` flags.

Use a separate `ScheduledMessagePayload` record for the full message:

```csharp
public sealed record ScheduledMessagePayload(
    string Body,
    string? ContentType,
    string? CorrelationId,
    string? MessageId,
    string? SessionId,
    string? Subject,
    string? To,
    string? ReplyTo,
    string? ReplyToSessionId,
    string? PartitionKey,
    TimeSpan? TimeToLive,
    IReadOnlyDictionary<string, ScheduledMessagePropertyValue> Properties);

public sealed record ScheduledMessagePropertyValue(string Type, string Value);
```

`ScheduledMessagePropertyValue` must round-trip the Service Bus-supported primitives used by BusLane rather than flattening every property to a string.

**Step 2: Run the focused tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Models.ModelTests&FullyQualifiedName~ScheduledMessageIndexEntry"
```

Expected: FAIL because the new domain members do not exist.

**Step 3: Implement the minimal domain**

Replace the positional legacy record with a property-based record that has JSON-safe defaults. Add:

```csharp
public bool HasPayload => !string.IsNullOrWhiteSpace(EncryptedPayload);
public bool IsLegacyLimited => SchemaVersion < CurrentSchemaVersion || !HasPayload;
public bool IsBrokerConfirmed =>
    Status is ScheduledMessageRecordStatus.Cancelled or ScheduledMessageRecordStatus.Rescheduled;
```

Keep legacy JSON property names readable. Do not store connection strings, credentials, or unencrypted bodies.

**Step 4: Run model tests**

Run the focused command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Models/ScheduledMessageIndexEntry.cs BusLane.Tests/Models/ModelTests.cs
rtk git commit -m "feat: define scheduled message lifecycle records"
```

### Task 2: Make the scheduled-message store encrypted, versioned, and mutation-safe

**Files:**
- Modify: `BusLane/Services/ServiceBus/ScheduledMessageStore.cs`
- Modify: `BusLane.Tests/Services/ServiceBus/ScheduledMessageStoreTests.cs`

**Step 1: Write failing store tests**

Add tests:

```csharp
[Fact]
public async Task AddAsync_WithPayload_PersistsEncryptedPayloadWithoutPlainBody()

[Fact]
public async Task LoadAsync_WithLegacyRecord_ReturnsLimitedEntry()

[Fact]
public async Task UpdateAsync_ConcurrentMutations_DoNotLoseEntries()

[Fact]
public async Task ResolveAsync_MarksEntryResolvedLocallyWithoutBrokerConfirmation()

[Fact]
public async Task LoadAsync_WithUndecryptablePayload_ReturnsStaleLimitedEntry()
```

Use an `IEncryptionService` substitute for deterministic encryption/decryption. Verify raw JSON does not contain the body or property values. Preserve existing owner-only-permission and cancellation tests.

**Step 2: Run store tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.ServiceBus.ScheduledMessageStoreTests"
```

Expected: FAIL because the store does not encrypt payloads or support lifecycle updates.

**Step 3: Extend the store contract**

Use this contract:

```csharp
public interface IScheduledMessageStore
{
    Task<IReadOnlyList<ScheduledMessageIndexEntry>> LoadAsync(CancellationToken ct = default);
    Task AddAsync(
        ScheduledMessageIndexEntry entry,
        ScheduledMessagePayload? payload = null,
        CancellationToken ct = default);
    Task UpdateAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default);
    Task<ScheduledMessagePayload?> LoadPayloadAsync(
        ScheduledMessageIndexEntry entry,
        CancellationToken ct = default);
}
```

Inject `IEncryptionService` and `TimeProvider`. Serialize the payload separately, encrypt it, and store only ciphertext on the record. Under one per-instance `SemaphoreSlim`, use one private read method and one private secure-write method so mutations never reacquire the same gate.

Load legacy positional records through a private legacy DTO and map them to schema version 1, limited entries. A payload decryption failure returns `null` from `LoadPayloadAsync`; it must not remove the record.

Use `AppPaths.CreateSecureFile` for atomic owner-only persistence.

**Step 4: Run store tests**

Run the focused command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Services/ServiceBus/ScheduledMessageStore.cs BusLane.Tests/Services/ServiceBus/ScheduledMessageStoreTests.cs
rtk git commit -m "feat: secure scheduled message index payloads"
```

### Task 3: Record enriched schedules from Send Message

**Files:**
- Modify: `BusLane/ViewModels/SendMessageViewModel.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane.Tests/ViewModels/SendMessageViewModelTests.cs`
- Modify: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`

**Step 1: Write failing scheduling tests**

Add or update tests:

```csharp
[Fact]
public async Task SendAsync_WithSchedule_IndexesConnectionContextAndFullPayload()

[Fact]
public async Task SendAsync_WhenIndexWriteFails_ReportsBrokerSuccessAndIndexWarning()

[Fact]
public void OpenSendMessagePopup_WithSavedConnection_PassesScheduledConnectionContext()
```

Define a small context record:

```csharp
public sealed record ScheduledMessageConnectionContext(
    string ConnectionId,
    string ConnectionName,
    string NamespaceEndpoint,
    ConnectionEnvironment Environment,
    ScheduledMessageConnectionKind Kind,
    string? NamespaceResourceId = null);
```

Assert the store receives the full payload separately from the index metadata. The status must be:

```text
Message scheduled successfully (sequence 42). The local schedule index could not be updated.
```

when the broker succeeds and persistence fails.

**Step 2: Run the focused tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.SendMessageViewModelTests|FullyQualifiedName~BusLane.Tests.ViewModels.MainWindowViewModelTests"
```

Expected: FAIL on the enriched store call and warning.

**Step 3: Implement Send Message recording**

Pass connection context into `SendMessageViewModel`. Build it in `MainWindowViewModel` from:

- `ActiveTab.SavedConnection` for saved connection-string tabs;
- `ActiveTab.Namespace` for Azure credential tabs;
- the current entity and optional subscription from navigation.

After `ScheduleMessageAsync` succeeds, create the record and payload and call `AddAsync`. Catch index-write failure separately, log only identifiers, and report the broker success plus warning. Keep immediate send behavior unchanged.

Add `PopulateFromScheduledPayload(ScheduledMessagePayload payload)` that clears the scheduled time and assigns a fresh message ID so the later clone action can reuse the send dialog.

**Step 4: Run the focused tests**

Run the command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/ViewModels/SendMessageViewModel.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane.Tests/ViewModels/SendMessageViewModelTests.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs
rtk git commit -m "feat: index complete scheduled send payloads"
```

### Task 4: Record scheduled replay results

**Files:**
- Modify: `BusLane/Models/MessageReplay.cs`
- Modify: `BusLane/Services/ServiceBus/MessageReplayService.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane.Tests/Services/ServiceBus/MessageReplayServiceTests.cs`
- Modify: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`

**Step 1: Write failing replay tests**

Add:

```csharp
[Fact]
public async Task ReplayAsync_WithScheduledRequest_RecordsEnrichedSchedule()

[Fact]
public async Task ReplayAsync_WhenIndexWriteFails_ReturnsBrokerSuccessWithWarning()
```

Extend `ReplayDestination` with the scheduled connection context. Assert scheduled replay records the same payload shape as Send Message.

**Step 2: Run focused replay tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.ServiceBus.MessageReplayServiceTests"
```

Expected: FAIL because replay does not write the schedule index.

**Step 3: Implement shared replay recording**

Inject `IScheduledMessageStore?` and `TimeProvider` into `MessageReplayService`. After the broker returns a sequence number, write the enriched record and encrypted payload. Merge any persistence warning into `ReplayResult.AuditWarning` without changing `IsSuccess`.

Update `MainWindowViewModel.GetReplayDestinations()` to carry the active tab's saved-connection or Azure-namespace identity.

**Step 4: Run replay tests**

Run the command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Models/MessageReplay.cs BusLane/Services/ServiceBus/MessageReplayService.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane.Tests/Services/ServiceBus/MessageReplayServiceTests.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs
rtk git commit -m "feat: index scheduled replay payloads"
```

### Task 5: Implement connection resolution and broker lifecycle actions

**Files:**
- Create: `BusLane/Services/ServiceBus/ScheduledMessageManagementService.cs`
- Create: `BusLane.Tests/Services/ServiceBus/ScheduledMessageManagementServiceTests.cs`

**Step 1: Write failing management-service tests**

Cover:

```csharp
[Fact]
public async Task RefreshAsync_ResolvesSavedConnectionAndDerivesUpcomingState()

[Fact]
public async Task RefreshAsync_MissingConnection_ReturnsStaleState()

[Fact]
public async Task CancelAsync_WithoutConfirmation_DoesNotCallBroker()

[Fact]
public async Task CancelAsync_ProductionWithoutAcknowledgement_DoesNotCallBroker()

[Fact]
public async Task CancelAsync_Success_MarksBrokerConfirmedCancellation()

[Fact]
public async Task RescheduleAsync_Success_CancelsThenSchedulesAndLinksReplacement()

[Fact]
public async Task RescheduleAsync_WhenReplacementFails_PreservesConfirmedCancellation()

[Fact]
public async Task ResolveAsync_MarksOnlyLocalResolution()
```

Use NSubstitute for `IConnectionStorageService`, `IServiceBusOperationsFactory`, `IAzureAuthService`, `IScheduledMessageStore`, and `IServiceBusOperations`.

**Step 2: Run tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.ServiceBus.ScheduledMessageManagementServiceTests"
```

Expected: FAIL because the service does not exist.

**Step 3: Implement the service**

Create request/result records:

```csharp
public sealed record ScheduledMessageActionRequest(
    ScheduledMessageIndexEntry Entry,
    bool IsConfirmed,
    bool IsProductionAcknowledged,
    DateTimeOffset? NewScheduledTime = null);

public sealed record ScheduledMessageActionResult(
    bool IsSuccess,
    string Message,
    ScheduledMessageIndexEntry Entry,
    bool IsPartialFailure = false);
```

Implement:

```csharp
Task<IReadOnlyList<ScheduledMessageResolvedEntry>> RefreshAsync(CancellationToken ct = default);
Task<ScheduledMessageActionResult> CancelAsync(ScheduledMessageActionRequest request, CancellationToken ct = default);
Task<ScheduledMessageActionResult> RescheduleAsync(ScheduledMessageActionRequest request, CancellationToken ct = default);
Task ResolveLocallyAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default);
Task<ScheduledMessagePayload?> LoadPayloadAsync(ScheduledMessageIndexEntry entry, CancellationToken ct = default);
```

Resolution rules:

- saved connection: `GetConnectionAsync`, then `CreateFromConnectionString`;
- Azure credential: require current authentication and matching namespace metadata, then `CreateFromAzureCredential`;
- missing or ambiguous connection: stale, no broker action.

Reschedule must persist confirmed cancellation before attempting the replacement. On success, add a new linked record and mark the old record `Rescheduled`. On replacement failure, leave the old record `Cancelled`, record the error, and return `IsPartialFailure = true`.

**Step 4: Run management-service tests**

Run the command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Services/ServiceBus/ScheduledMessageManagementService.cs BusLane.Tests/Services/ServiceBus/ScheduledMessageManagementServiceTests.cs
rtk git commit -m "feat: manage scheduled message broker lifecycle"
```

### Task 6: Build filtering, calendar projection, and action state in the view model

**Files:**
- Create: `BusLane/ViewModels/ScheduledMessagesViewModel.cs`
- Create: `BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs`

**Step 1: Write failing view-model tests**

Add tests for:

```csharp
[Fact]
public async Task RefreshAsync_LoadsResolvedEntriesAndDefaultsToUpcoming()

[Theory]
[InlineData("orders")]
[InlineData("message-42")]
[InlineData("corr-42")]
[InlineData("tenant")]
[InlineData("north")]
public async Task SearchText_MatchesSupportedMetadata(string searchText)

[Fact]
public async Task Filters_CombineConnectionEntityEnvironmentStatusAndTimeRange()

[Fact]
public async Task CalendarDays_ProjectTheSameFilteredEntries()

[Fact]
public async Task CloneAsync_LegacyEntry_ShowsPayloadUnavailable()

[Fact]
public async Task CancelAsync_ProductionEntry_RequiresAcknowledgement()

[Fact]
public async Task RescheduleAsync_PastTime_ShowsValidationError()

[Fact]
public async Task ResolveAsync_RefreshesCollectionWithoutBrokerClaim()
```

**Step 2: Run view-model tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.ScheduledMessagesViewModelTests"
```

Expected: FAIL because the view model does not exist.

**Step 3: Implement minimal view-model behavior**

Use `ObservableCollection<ScheduledMessageItemViewModel>` and derived `FilteredEntries` plus `CalendarDays`. Add observable properties for:

- search text;
- selected connection/entity/environment/status/time range;
- list/calendar mode;
- selected month and selected entry;
- loading, empty, error, and status text;
- confirmation action, confirmation text, production acknowledgment;
- reschedule timestamp and validation.

Commands:

```text
Refresh, ShowList, ShowCalendar, PreviousMonth, NextMonth,
Clone, BeginCancel, BeginReschedule, ConfirmAction, CancelAction, Resolve
```

Pass a clone callback that receives the resolved entry and decrypted payload. Do not place broker logic directly in the view model.

**Step 4: Run view-model tests**

Run the command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/ViewModels/ScheduledMessagesViewModel.cs BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs
rtk git commit -m "feat: add scheduled message console state"
```

### Task 7: Create the list/calendar console UI

**Files:**
- Create: `BusLane/Views/Controls/ScheduledMessagesView.axaml`
- Create: `BusLane/Views/Controls/ScheduledMessagesView.axaml.cs`
- Create: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`

**Step 1: Write failing XAML contract tests**

Assert the view contains:

- search and connection/entity/environment/status/time filters;
- list/calendar toggle commands;
- list columns for time, connection, entity, metadata, local state, broker state, and actions;
- calendar previous/next navigation and day entry bindings;
- clone, cancel, reschedule, refresh, and resolve commands;
- local-index and broker-confirmed labels;
- a confirmation overlay with environment, entity, time, sequence, and production acknowledgment;
- partial-failure and empty-state surfaces.

**Step 2: Run view tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.ScheduledMessagesViewTests"
```

Expected: FAIL because the XAML does not exist.

**Step 3: Implement the Avalonia view**

Follow existing Fluent 2 resource names and compiled-binding conventions. Keep the panel header in `MainWindow.axaml`; the control starts with filters and view controls. Use a `DataGrid`-style list only if the project already references and styles it; otherwise use an `ItemsControl` with a fixed grid template to avoid adding dependencies.

The calendar is a seven-column `ItemsControl` backed by view-model day cells. Use text and icons together for action buttons; color alone must not convey broker status.

**Step 4: Run view tests**

Run the command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/Views/Controls/ScheduledMessagesView.axaml BusLane/Views/Controls/ScheduledMessagesView.axaml.cs BusLane.Tests/Views/ScheduledMessagesViewTests.cs
rtk git commit -m "feat: add scheduled message console views"
```

### Task 8: Integrate the global feature panel, navigation, clone workflow, and DI

**Files:**
- Modify: `BusLane/ViewModels/Core/FeaturePanelsViewModel.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane/Views/MainWindow.axaml`
- Modify: `BusLane/Views/Controls/NavigationSidebar.axaml`
- Modify: `BusLane/Program.cs`
- Modify: `BusLane.Tests/ViewModels/Core/FeaturePanelsViewModelTests.cs`
- Modify: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Modify: `BusLane.Tests/Views/NavigationSidebarTests.cs`
- Modify: `BusLane.Tests/Views/MainWindowViewTests.cs`
- Modify: `BusLane.Tests/ProgramCompositionTests.cs`

**Step 1: Write failing integration tests**

Add assertions that:

- `OpenScheduledMessagesCommand` creates and refreshes the console;
- opening it closes Live Stream, Correlation Explorer, Charts, and Alerts;
- `CloseAll` closes and clears it;
- clone resolves the correct operations and opens `SendMessageViewModel` with payload data and no schedule;
- expanded and rail sidebars each expose one Scheduled Messages command;
- `MainWindow.axaml` hosts the feature panel and close command;
- DI resolves `IScheduledMessageManagementService` and the encrypted store.

**Step 2: Run integration tests and verify failure**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~FeaturePanelsViewModelTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~NavigationSidebarTests|FullyQualifiedName~MainWindowViewTests|FullyQualifiedName~ProgramCompositionTests"
```

Expected: FAIL on missing console integration.

**Step 3: Implement integration**

In `FeaturePanelsViewModel`, add:

```csharp
[ObservableProperty] private bool _showScheduledMessages;
[ObservableProperty] private ScheduledMessagesViewModel? _scheduledMessagesViewModel;
```

Add open/close lifecycle alongside the existing mutually exclusive panels.

In `MainWindowViewModel`, add relay commands that delegate to `FeaturePanels`. The clone callback must open the send dialog with operations resolved for the indexed connection, call `PopulateFromScheduledPayload`, and leave `ScheduledEnqueueTimeText` empty.

Add expanded and collapsed sidebar buttons using a calendar/clock Lucide icon. Add the panel host in `MainWindow.axaml`.

Register:

```csharp
services.AddSingleton<IScheduledMessageManagementService, ScheduledMessageManagementService>();
```

Construct `ScheduledMessageStore` with `IEncryptionService` and `TimeProvider`, and pass the management service to `MainWindowViewModel`.

**Step 4: Run integration tests**

Run the command from Step 2.

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/ViewModels/Core/FeaturePanelsViewModel.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane/Views/MainWindow.axaml BusLane/Views/Controls/NavigationSidebar.axaml BusLane/Program.cs BusLane.Tests/ViewModels/Core/FeaturePanelsViewModelTests.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs BusLane.Tests/Views/NavigationSidebarTests.cs BusLane.Tests/Views/MainWindowViewTests.cs BusLane.Tests/ProgramCompositionTests.cs
rtk git commit -m "feat: integrate scheduled message management console"
```

### Task 9: Update parity documentation and verify the complete feature

**Files:**
- Modify: `docs/servicebus-parity-guide.md`
- Modify if required by implementation: `README.md`

**Step 1: Update documentation**

Document:

- only messages scheduled through BusLane are indexed;
- full payloads for new entries are encrypted locally;
- legacy records have limited actions;
- refresh cannot verify individual broker schedules;
- broker-confirmed and local-only states are different;
- partial reschedule failure behavior.

Add a concise README feature bullet only if the README's existing feature list has room without duplicating the parity guide.

**Step 2: Run formatting and whitespace checks**

Run:

```bash
rtk dotnet build
rtk git diff --check
```

Expected: build succeeds and no whitespace errors are reported.

**Step 3: Run focused feature tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessage|FullyQualifiedName~SendMessageViewModelTests|FullyQualifiedName~MessageReplayServiceTests|FullyQualifiedName~FeaturePanelsViewModelTests|FullyQualifiedName~ProgramCompositionTests"
```

Expected: PASS.

**Step 4: Run the full suite**

Run:

```bash
rtk dotnet test
```

Expected: PASS with zero failures.

**Step 5: Review scope**

Run:

```bash
rtk git status --short
rtk git diff --stat main...HEAD
rtk git log --oneline main..HEAD
```

Confirm every changed file maps to issue #160 and no credentials, connection strings, tokens, plaintext indexed payloads, or unrelated cleanup are present.

**Step 6: Commit documentation**

```bash
rtk git add docs/servicebus-parity-guide.md README.md
rtk git commit -m "docs: explain scheduled message console limits"
```

If `README.md` is unchanged, omit it from `git add`.

### Task 10: Final implementation review

**Files:**
- Review all files changed by `main...HEAD`

**Step 1: Invoke required review skills**

Use:

- `superpowers:verification-before-completion`;
- `superpowers:requesting-code-review`.

Do not claim completion until both workflows finish and any actionable findings are resolved.

**Step 2: Re-run verification after review fixes**

Run:

```bash
rtk dotnet build
rtk dotnet test
rtk git diff --check main...HEAD
```

Expected: all commands succeed.

**Step 3: Confirm clean handoff**

Run:

```bash
rtk git status --short --branch
```

Expected: the issue branch is clean and ahead of `main` by the feature commits.
