# Utility Navigation Redesign

## Problem

The expanded navigation sidebar currently gives six utility destinations equal, persistent space. As the list grows, the fixed utility dock consumes too much vertical room and competes with the workspace and quick-switch content above it.

## Approved direction

Use a compact priority dock with progressive disclosure.

- Keep **Dashboard**, **Alerts**, and **Settings** permanently visible because they are the most frequently used destinations.
- Move **Live Stream**, **Correlation Explorer**, and **Scheduled Messages** into a **More tools** section.
- Keep the utility area anchored to the bottom of the expanded sidebar.
- Expand the secondary tools directly above the pinned destinations so the user's spatial context does not change.
- Start with More tools collapsed whenever a new main-window view model is created.
- Preserve the existing collapsed sidebar rail shortcuts and all existing feature-panel commands.

## Layout

From top to bottom, the expanded utility dock contains:

1. An optional expanded secondary-tools panel.
2. A labeled **More tools** toggle with a grid icon and chevron.
3. A divider.
4. Pinned rows for **Dashboard**, **Alerts**, and **Settings**.

The Alerts row retains its unread-count badge. All rows remain full-width, keyboard-focusable controls with icons and visible labels.

## Interaction

- Selecting More tools toggles the secondary panel without navigating.
- The chevron communicates expanded or collapsed state.
- Selecting a secondary tool uses its existing command and leaves the expansion state unchanged.
- Keyboard focus order follows the visual order: secondary tools when expanded, More tools, Dashboard, Alerts, Settings.
- The toggle exposes its expanded state through Avalonia's native `ToggleButton` semantics.

## Visual treatment

- Reuse the current sidebar surfaces, typography, Lucide icons, spacing scale, and alert badge.
- Reduce the pinned row padding slightly so the dock stays compact without shrinking the interactive target below 44 pixels.
- Give the expanded secondary-tools group a subtle inset surface and border so it reads as content revealed by More tools.
- Use the existing brand focus treatment; do not rely on color alone to communicate expansion.

## State and architecture

Add a single `IsMoreToolsExpanded` observable UI-state property to `MainWindowViewModel`. Bind the More tools toggle to it and bind the secondary-tools container visibility to the same property. No feature-panel command, navigation route, service, or persistence behavior changes.

The state is intentionally not persisted. This keeps the default sidebar compact at application start and avoids adding a preference for a transient disclosure control.

## Verification

- Add structural XAML tests proving that the three frequent destinations remain outside the collapsible container.
- Verify that all three secondary destinations are inside a container bound to `IsMoreToolsExpanded`.
- Verify that the More tools toggle is two-way bound to the same property and has an accessible label.
- Preserve the existing tests that require scheduled messages and correlation explorer shortcuts in both expanded and collapsed sidebar modes.
- Run the focused navigation-sidebar tests, then the full test suite and build.

## Out of scope

- Reordering or customizing pinned utilities.
- Persisting the expanded state.
- Adding search or a command launcher.
- Changing the collapsed rail.
- Changing feature-panel content or navigation behavior.
