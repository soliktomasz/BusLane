# Collapsible Entity Pane Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let users hide the inner entity pane per connection tab, expand the messages workspace, and restore that choice after restart.

**Architecture:** Persist a per-tab entity-pane visibility flag on `ConnectionTabViewModel` and round-trip it through `TabSessionState`. Expose the active tab's visibility through `MainWindowViewModel`, use explicit hide and restore commands to save the session immediately, and update the Azure and connection-string workspace grids so a slim restore handle replaces the entity pane when hidden.

**Tech Stack:** .NET 10, Avalonia, CommunityToolkit.Mvvm, xUnit, FluentAssertions, NSubstitute

---

### Task 1: Persist entity-pane visibility in tab session state

**Files:**
- Modify: `BusLane.Tests/Models/TabSessionStateTests.cs`
- Modify: `BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs`
- Modify: `BusLane/Models/TabSessionState.cs`
- Modify: `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`

**Step 1: Write the failing tests**
- Extend `TabSessionStateTests` with a case that stores `IsEntityPaneVisible = false` and asserts the value is preserved.
- Extend `ConnectionTabViewModelTests` with:
  - a default-state test asserting new tabs start with the entity pane visible
  - an `ApplySessionState` test asserting a restored tab picks up the persisted hidden state
  - a `CreateSessionState` test asserting the saved state includes the current visibility flag

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TabSessionStateTests|FullyQualifiedName~ConnectionTabViewModelTests"`

Expected: FAIL because the session model and tab view-model do not yet expose entity-pane visibility.

**Step 3: Write minimal implementation**
- Add `IsEntityPaneVisible` to `TabSessionState`.
- Add `IsEntityPaneVisible` to `ConnectionTabViewModel` with a default value of `true`.
- Update `CreateSessionState(...)` to serialize the current visibility.
- Update `ApplySessionState(...)` to restore the persisted value and treat older state objects without the field as visible.

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TabSessionStateTests|FullyQualifiedName~ConnectionTabViewModelTests"`

Expected: PASS

**Step 5: Commit**

```bash
git add BusLane/Models/TabSessionState.cs BusLane/ViewModels/Core/ConnectionTabViewModel.cs BusLane.Tests/Models/TabSessionStateTests.cs BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs
git commit -m "feat: persist per-tab entity pane visibility"
```

### Task 2: Add active-tab toggle commands and explicit session saving

**Files:**
- Create: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`

**Step 1: Write the failing tests**
- Add a test that hides the entity pane for the active tab and asserts:
  - the active tab visibility becomes `false`
  - the session JSON is updated through `Tabs.SaveTabSession()`
- Add a test that restores the pane and asserts:
  - the active tab visibility becomes `true`
  - a second tab's visibility is unchanged
- Add a test that `MainWindowViewModel` reports the active tab's visibility through a computed property used by the view.

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~MainWindowViewModelTests"`

Expected: FAIL because the computed property and hide or restore commands do not exist yet.

**Step 3: Write minimal implementation**
- Add a computed property such as `IsCurrentEntityPaneVisible` to `MainWindowViewModel`.
- Add relay commands such as `HideEntityPaneCommand` and `ShowEntityPaneCommand`.
- Make the commands no-op when there is no active tab.
- Have each command update `ActiveTab.IsEntityPaneVisible`, then call `Tabs.SaveTabSession()` so restart persistence is explicit and immediate.
- Raise property-changed notifications for the computed visibility when the active tab changes and when the active tab's visibility property changes.

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~MainWindowViewModelTests"`

Expected: PASS

**Step 5: Commit**

```bash
git add BusLane/ViewModels/MainWindowViewModel.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs
git commit -m "feat: add active-tab entity pane toggle commands"
```

### Task 3: Update the workspace layout and entity panes

**Files:**
- Modify: `BusLane/Views/MainWindow.axaml`
- Modify: `BusLane/Views/MainWindow.axaml.cs`
- Modify: `BusLane/Views/Controls/AzureEntityTreeView.axaml`
- Modify: `BusLane/Views/Controls/EntityTreeView.axaml`
- Modify: `BusLane/Styles/AppStyles.axaml`

**Step 1: Write the minimal UI changes**
- Add a hide control to the header area of both entity-pane views and bind it to the new hide command.
- Update both workspace grids in `MainWindow.axaml` so they include:
  - a slim restore-handle column
  - the entity-pane column
  - the messages column
- Show the restore handle only when the active tab's entity pane is hidden.
- Keep the messages panel in place so the currently selected entity and loaded messages stay intact.
- Add only the smallest styling needed for the slim restore handle so it feels intentional and usable.

**Step 2: Wire layout updates in code-behind**
- Extend `MainWindow.axaml.cs` with an `ApplyEntityPaneLayoutColumns()` helper similar in spirit to the existing terminal row layout helper.
- Re-run that helper when:
  - the data context changes
  - the active tab changes
  - the active tab's entity-pane visibility changes
- Set column widths so visible mode matches today's proportions, while hidden mode collapses the entity pane and expands the messages pane.

**Step 3: Run build verification**

Run: `dotnet build`

Expected: PASS

**Step 4: Commit**

```bash
git add BusLane/Views/MainWindow.axaml BusLane/Views/MainWindow.axaml.cs BusLane/Views/Controls/AzureEntityTreeView.axaml BusLane/Views/Controls/EntityTreeView.axaml BusLane/Styles/AppStyles.axaml
git commit -m "feat: add collapsible entity pane layout"
```

### Task 4: Run regression verification and manual workflow checks

**Files:**
- No new files expected

**Step 1: Run targeted tests**

Run: `dotnet test --filter "FullyQualifiedName~TabSessionStateTests|FullyQualifiedName~ConnectionTabViewModelTests|FullyQualifiedName~MainWindowViewModelTests"`

Expected: PASS

**Step 2: Run full build**

Run: `dotnet build`

Expected: PASS

**Step 3: Run full test suite**

Run: `dotnet test`

Expected: PASS

**Step 4: Manual verification**
- Open a connection-string tab, hide the entity pane, and confirm the messages pane expands.
- Restore the pane using the slim handle and confirm the previous entity selection is still present.
- Open a second tab and confirm it keeps its own pane visibility independent from the first tab.
- Repeat the hide and restore flow in Azure namespace mode.
- Restart the app and confirm each restored tab keeps its own saved entity-pane visibility.

**Step 5: Commit only if verification required code changes**
- If the verification steps above pass without further edits, do not create an extra verification-only commit.
- If verification exposes regressions that require fixes, commit those fixes with a focused message after rerunning the same build and test commands.
