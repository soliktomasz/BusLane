# Namespace Priority Inbox Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a single-namespace triage inbox that ranks entities by urgency, shows since-last-check deltas, and jumps directly into existing investigation workflows.

**Architecture:** Introduce a dedicated inbox service and `NamespaceInboxViewModel` that compose existing queue, subscription, alert, metrics-history, and navigation data. Host the inbox inside the namespace dashboard surface so the feature reuses current refresh and message investigation flows instead of creating a parallel UI stack.

**Tech Stack:** C#, Avalonia UI, CommunityToolkit.Mvvm, xUnit, FluentAssertions, NSubstitute

---

### Task 1: Add inbox scoring model tests

**Files:**
- Create: `BusLane.Tests/Services/Monitoring/NamespaceInboxScoringServiceTests.cs`
- Create: `BusLane/Services/Monitoring/INamespaceInboxScoringService.cs`
- Create: `BusLane/Services/Monitoring/NamespaceInboxScoringService.cs`
- Create: `BusLane/Models/NamespaceInboxItem.cs`

**Step 1: Write the failing test**

Write focused tests that assert:

- entities with rising dead-letter counts rank above stable entities
- active unacknowledged alerts increase score and appear in reasons
- queue scheduled counts contribute to queue urgency without affecting subscriptions
- rows are returned in descending score order

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Monitoring.NamespaceInboxScoringServiceTests"`

Expected: FAIL because the scoring service and inbox model do not exist yet.

**Step 3: Write minimal implementation**

Create a deterministic scoring service that accepts current queues, subscriptions, alert data, and metrics-history comparisons, then returns ranked `NamespaceInboxItem` results with score and reasons.

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Monitoring.NamespaceInboxScoringServiceTests"`

Expected: PASS

### Task 2: Add review-state persistence tests

**Files:**
- Create: `BusLane.Tests/Services/Monitoring/NamespaceInboxReviewStoreTests.cs`
- Create: `BusLane/Services/Monitoring/INamespaceInboxReviewStore.cs`
- Create: `BusLane/Services/Monitoring/NamespaceInboxReviewStore.cs`
- Modify: `BusLane/Services/Infrastructure/AppPaths.cs`

**Step 1: Write the failing test**

Write tests that assert:

- review state is saved and loaded by namespace/entity key
- reviewed snapshot values are preserved for delta calculation
- missing or corrupt files return an empty store instead of throwing

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Monitoring.NamespaceInboxReviewStoreTests"`

Expected: FAIL because the review store does not exist yet.

**Step 3: Write minimal implementation**

Add a simple JSON-backed review store using existing safe serializer patterns and a dedicated app-path entry for inbox review state.

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Monitoring.NamespaceInboxReviewStoreTests"`

Expected: PASS

### Task 3: Add inbox view model tests

**Files:**
- Create: `BusLane.Tests/ViewModels/Dashboard/NamespaceInboxViewModelTests.cs`
- Create: `BusLane/ViewModels/Dashboard/NamespaceInboxViewModel.cs`
- Create: `BusLane/ViewModels/Dashboard/NamespaceInboxItemViewModel.cs`

**Step 1: Write the failing test**

Write tests that assert:

- refreshing the inbox builds rows from queues and subscriptions
- marking an item reviewed updates delta baseline
- opening DLQ/messages/session actions invoke the expected navigation callbacks
- entities with no historical data still appear with neutral deltas

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.Dashboard.NamespaceInboxViewModelTests"`

Expected: FAIL because the inbox view models do not exist yet.

**Step 3: Write minimal implementation**

Create a dashboard-scoped inbox view model that delegates ranking to the scoring service, exposes observable row view models, and invokes shell callbacks for investigation jumps.

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.Dashboard.NamespaceInboxViewModelTests"`

Expected: PASS

### Task 4: Integrate inbox into namespace dashboard refresh flow

**Files:**
- Modify: `BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs`
- Modify: `BusLane/Services/Dashboard/DashboardRefreshService.cs`
- Modify: `BusLane/Services/Dashboard/IDashboardRefreshService.cs`
- Modify: `BusLane/Program.cs`

**Step 1: Add refresh integration**

Extend the dashboard refresh pipeline to surface queue and subscription snapshots needed by the inbox without duplicating namespace fetches.

**Step 2: Wire dependencies**

Register the inbox services and view model in dependency injection, then compose `NamespaceInboxViewModel` into `NamespaceDashboardViewModel`.

**Step 3: Build and run targeted tests**

Run: `dotnet test --filter "FullyQualifiedName~NamespaceInbox"`

Expected: PASS for the new inbox-focused test classes.

### Task 5: Add the inbox UI

**Files:**
- Create: `BusLane/Views/Controls/NamespaceInboxView.axaml`
- Create: `BusLane/Views/Controls/NamespaceInboxView.axaml.cs`
- Modify: `BusLane/Views/Controls/NamespaceDashboardView.axaml`

**Step 1: Add the view**

Build a compact inbox list with:

- entity name and type
- score badge or priority label
- reason chips or short text
- since-last-check deltas
- action buttons for open messages, open DLQ, and open session inspector

**Step 2: Bind it into the dashboard**

Place the inbox above the top-entities lists and charts in `NamespaceDashboardView.axaml`, with loading and empty states.

**Step 3: Build to verify bindings compile**

Run: `dotnet build`

Expected: PASS

### Task 6: Connect inbox actions to existing navigation and message flows

**Files:**
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`
- Modify: `BusLane/ViewModels/Core/NavigationState.cs`
- Modify: `BusLane/ViewModels/Core/MessageOperationsViewModel.cs`

**Step 1: Add investigation callbacks**

Expose the minimal callbacks required for the inbox to select a queue or subscription, switch to active or dead-letter message tabs, and open session inspection when appropriate.

**Step 2: Keep navigation behavior consistent**

Reuse current selection and message-loading behavior rather than adding separate message retrieval logic inside the inbox.

**Step 3: Run focused tests**

Run: `dotnet test --filter "FullyQualifiedName~NamespaceInboxViewModelTests|FullyQualifiedName~MessageOperationsViewModelTests|FullyQualifiedName~SessionInspectorViewModelTests"`

Expected: PASS

### Task 7: Final verification

**Files:**
- Test: `BusLane.Tests/Services/Monitoring/NamespaceInboxScoringServiceTests.cs`
- Test: `BusLane.Tests/Services/Monitoring/NamespaceInboxReviewStoreTests.cs`
- Test: `BusLane.Tests/ViewModels/Dashboard/NamespaceInboxViewModelTests.cs`

**Step 1: Run inbox-focused tests**

Run: `dotnet test --filter "FullyQualifiedName~NamespaceInbox"`

Expected: PASS

**Step 2: Run full build**

Run: `dotnet build`

Expected: PASS

**Step 3: Commit**

```bash
git add BusLane/Models/NamespaceInboxItem.cs BusLane/Program.cs BusLane/Services/Dashboard/DashboardRefreshService.cs BusLane/Services/Dashboard/IDashboardRefreshService.cs BusLane/Services/Infrastructure/AppPaths.cs BusLane/Services/Monitoring/INamespaceInboxReviewStore.cs BusLane/Services/Monitoring/INamespaceInboxScoringService.cs BusLane/Services/Monitoring/NamespaceInboxReviewStore.cs BusLane/Services/Monitoring/NamespaceInboxScoringService.cs BusLane/ViewModels/Core/MessageOperationsViewModel.cs BusLane/ViewModels/Core/NavigationState.cs BusLane/ViewModels/Dashboard/NamespaceDashboardViewModel.cs BusLane/ViewModels/Dashboard/NamespaceInboxItemViewModel.cs BusLane/ViewModels/Dashboard/NamespaceInboxViewModel.cs BusLane/Views/Controls/NamespaceDashboardView.axaml BusLane/Views/Controls/NamespaceInboxView.axaml BusLane/Views/Controls/NamespaceInboxView.axaml.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane.Tests/Services/Monitoring/NamespaceInboxReviewStoreTests.cs BusLane.Tests/Services/Monitoring/NamespaceInboxScoringServiceTests.cs BusLane.Tests/ViewModels/Dashboard/NamespaceInboxViewModelTests.cs
git commit -m "feat: add namespace priority inbox"
```
