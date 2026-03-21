# Collapsible Entity Pane

Date: 2026-03-21

## Goal
Let users hide the inner entity pane beside messages so the message workspace can expand for focused browsing without losing the current entity or message context.

## Approved Direction
- Add a per-connection-tab inner entity pane visibility state.
- Default the pane to visible for new and previously restored tabs.
- Collapse the pane completely when hidden and leave a slim restore handle on the left edge of the workspace.
- Persist the hidden or shown state in tab session restore so each tab comes back the way the user left it.
- Apply the same behavior in both Azure namespace mode and connection-string mode.

## Scope
This change applies to:
- `BusLane/Views/MainWindow.axaml`
- `BusLane/Views/MainWindow.axaml.cs`
- `BusLane/Views/Controls/AzureEntityTreeView.axaml`
- `BusLane/Views/Controls/EntityTreeView.axaml`
- `BusLane/ViewModels/MainWindowViewModel.cs`
- `BusLane/ViewModels/Core/ConnectionTabViewModel.cs`
- `BusLane/Models/TabSessionState.cs`
- `BusLane.Tests/Models/TabSessionStateTests.cs`
- `BusLane.Tests/ViewModels/Core/ConnectionTabViewModelTests.cs`

## Interaction Model
- Hiding the pane is a layout-only action, not a navigation reset.
- The selected queue, topic, or subscription remains selected when the pane is hidden.
- The current message tab, loaded messages, search text, and session inspector state remain unchanged when the pane is hidden or restored.
- The hide action lives on the entity pane itself in both entity-pane variants.
- The restore action lives in a slim vertical handle shown at the left edge of the workspace only while the pane is hidden.
- The first pass should use an immediate collapse and restore rather than a custom animation.

## State And Persistence
- Store the visibility state on `ConnectionTabViewModel` as a per-tab boolean such as `IsEntityPaneVisible`.
- Keep the state out of the global preferences model because the user wants independent behavior per tab.
- Round-trip the value through `TabSessionState` so restored tabs reopen with the same pane visibility they had before shutdown.
- Treat missing persisted values from older sessions as `true` so existing users do not unexpectedly lose the entity pane after upgrade.
- Use an explicit save hook when the user hides or restores the pane so restart behavior does not depend on unrelated tab changes.

## Layout
- Keep the current outer app sidebar behavior unchanged. This feature applies only to the inner entity pane beside the messages panel.
- Convert the Azure-mode and connection-string-mode workspace grids into a three-part layout:
  - a slim restore-handle column
  - the entity pane column
  - the messages column
- When the entity pane is visible:
  - the restore-handle column is collapsed
  - the entity pane keeps the current proportional width
  - the messages pane keeps the current proportional width
- When the entity pane is hidden:
  - the restore-handle column becomes visible
  - the entity pane column collapses to zero width
  - the messages pane expands to fill the remaining space

## Non-Goals
- Do not add a broader full-screen or message-focus mode.
- Do not change the far-left navigation sidebar toggle behavior.
- Do not reload messages just because the entity pane layout changed.
- Do not redesign Live Stream, Charts, Alerts, or other overlays as part of this change.

## Accessibility And Usability
- Keep the restore handle keyboard reachable and visually distinct enough to discover without needing extra header buttons.
- Preserve existing keyboard shortcuts and message interactions.
- Keep hit targets large enough to avoid making the restore affordance fiddly.

## Verification
- `TabSessionState` tests confirm the new visibility flag round-trips safely.
- `ConnectionTabViewModel` tests confirm session state restore preserves the visibility flag.
- Build verification catches XAML bindings and generated command issues with `dotnet build`.
- Manual verification confirms:
  - hiding the pane enlarges the messages workspace
  - the restore handle brings the pane back
  - tab A can hide the pane while tab B remains visible
  - restarting the app restores the pane state per tab in both connection modes
