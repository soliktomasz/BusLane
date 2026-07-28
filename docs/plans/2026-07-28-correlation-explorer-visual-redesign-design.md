# Correlation Explorer Visual Redesign

## Goal

Bring the Correlation Explorer into BusLane's existing visual language while making correlation investigation easier to scan. The redesign will emphasize the chronological message flow, retain a dense message inspector, and preserve all filtering, comparison, replay, history, and live-update behavior.

This is a presentation-focused change. It does not alter correlation grouping, catalog ingestion, replay safety rules, message comparison, or persistence.

## Design Direction

The explorer will become a timeline-first investigation workspace:

- a compact correlation rail for choosing a group;
- a visually readable event timeline as the primary workspace;
- a focused inspector for payload, metadata, properties, comparisons, and replay history.

This direction fits BusLane better than retaining three equally weighted flat panes. It also avoids the complexity of a graph visualization, which would be less useful for precise chronological message inspection.

## Page Structure

The existing page-level Correlation Explorer header in `MainWindow` remains the single title and close surface. The duplicate header inside `CorrelationExplorerView` will be removed.

The explorer content will use three rows:

1. A compact command and search bar.
2. A collapsible structured-filter surface.
3. The main investigation workspace.

The command bar will use the same icon-led controls, spacing, and layered surfaces as the Messages and Live Stream views. Primary search remains immediately available. Filters, Refresh, and Export history use compact Lucide icon actions with text labels and tooltips.

## Correlation Rail

The left rail will be approximately 260 pixels wide and use a distinct surface separated from the timeline by the standard BusLane border.

Each correlation group row will present:

- the correlation or session identifier;
- a message-count badge;
- a correlation or session-fallback badge;
- a clear selected state using the existing selected background and accent border resources.

The rail will include a small section header and result count context when available. When no groups match, it will show an intentional empty state instead of a blank list.

## Timeline

The center column becomes the visual anchor. Timeline entries will read as connected chronological events rather than generic list rows.

Each event shows:

- enqueue time;
- entity name and entity type;
- loaded or live-stream source badge;
- environment context;
- message ID;
- compact A and B comparison-slot actions.

A vertical guide and event marker create a readable message path without introducing a new graph control. Selection uses BusLane's existing selected surface and accent treatment. The live-update notification remains non-intrusive in the timeline header.

If no group or timeline event is available, the center column displays a concise empty state with guidance.

## Message Inspector

The right inspector remains the widest and most information-dense area.

Its header shows the selected message ID, namespace, entity context, and relevant status badges. `Compare with previous` remains secondary, while `Replay selected message` remains the clear primary action.

The inspector keeps these tabs:

- Payload
- Metadata
- Application properties
- Compare
- Replay history

Payload continues to use the existing code-editor style. Metadata and application properties will use structured label/value rows rather than unformatted text. Comparison content will present the A and B identities first, followed by timing, metadata, property, and body differences. Replay history will use compact audit rows with clear outcome treatment.

Empty states will cover:

- no selected message;
- no active comparison;
- no metadata or property differences;
- no replay history.

## Filters

Primary text search stays visible in the command bar. The remaining filters appear in a collapsible layered surface under it.

Structured fields will use BusLane's `field-label` typography and a balanced grid instead of a long `WrapPanel` wall. The surface groups related criteria:

- time range;
- namespace and entity;
- environment and source;
- correlation or session identifier;
- application-property key and value.

Validation remains inline using the critical semantic color. Clear and Apply actions remain right aligned. Existing ViewModel commands and validation behavior are unchanged.

## Visual Language

The redesign will reuse existing resources wherever possible:

- `AppBackground`, `CardBackground`, `SurfaceSubtle`, and `LayerBackground`;
- `BorderDefault` and `BorderMuted`;
- `AccentBrand`, selected-state resources, and semantic status colors;
- existing typography, buttons, badges, icon surfaces, and code-editor styling;
- existing light and dark theme dictionaries.

Any new styles will be narrowly scoped to the explorer and added only when repeated visual behavior cannot be expressed cleanly with existing styles.

## Data Flow and Behavior

The redesign does not change the ViewModel or service architecture:

- `Groups` continues to drive the correlation rail.
- `SelectedGroup` continues to populate `Timeline`.
- `SelectedMessage` continues to drive the inspector.
- Existing filter commands and properties remain intact.
- Existing comparison-slot commands remain intact.
- Existing replay commands, dialog, and audit history remain intact.
- Live catalog updates continue to preserve selection and expose the new-message count.

No new persistence, network activity, replay capability, or catalog behavior is introduced.

## Error and Empty States

Existing `FilterValidationMessage` and `StatusMessage` values remain the error sources. Filter validation appears within the filter surface. Operational status appears in a compact information bar near the workspace without obscuring investigation content.

Blank lists and tabs will use descriptive empty states rather than silently rendering empty controls.

## Testing

The implementation will:

- update the Correlation Explorer XAML contract tests for the new structure;
- preserve assertions for groups, timeline, filtering, comparison, replay, history, and live updates;
- add structural assertions for the single-header layout, icon-led command bar, timeline treatment, and empty states;
- run focused Correlation Explorer view and ViewModel tests;
- run `dotnet build`;
- run the full `dotnet test` regression suite.

## Scope

In scope:

- `CorrelationExplorerView.axaml`;
- narrowly scoped explorer styles in `AppStyles.axaml` if required;
- XAML contract tests affected by the layout;
- removal of the duplicate inner header.

Out of scope:

- correlation or replay behavior changes;
- graph visualization;
- catalog capacity or persistence changes;
- new filtering criteria;
- cross-namespace replay;
- unrelated visual refactoring.
