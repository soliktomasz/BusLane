---
version: 1
slug: "buslane-views-controls-messagespanelview-axaml"
primary_target: "BusLane/Views/Controls/MessagesPanelView.axaml"
related_targets: ["BusLane/Views/MainWindow.axaml","BusLane/Views/Controls/MessageInspectorView.axaml","BusLane/Views/Controls/MessageRowView.axaml"]
---

# Focused desktop workspace

Mode: Operate. Users browse Azure Service Bus queues and subscriptions, then inspect messages without interrupting navigation. Preserve read-only peeking, explicit locked settlement, sessions, bulk operations, and both themes.

## Direction contract

THESIS: Keep entity, message, and payload context visible together instead of opening a modal for every row.

OWN-WORLD: Violet navigation, pale lavender light surfaces, graphite dark surfaces, Inter controls, thin seams, and aligned virtualized rows. Code alone uses monospace.

STORY: Choose an entity, scan subjects and IDs, select a message, and read or copy its body. Expand only for focused full metadata inspection.

FIRST VIEWPORT: A 56px tool rail, 240px entity pane, compact namespace tabs, flexible table, and 360px right inspector. Send remains the primary action. Narrow work areas stack the inspector when tall enough; narrow, short panes retain the table and explicit row details. Sessions, locked-receive, and bulk workflows regain the full width.

FORM: Approved light-concept arrangement, adapted to native Avalonia controls and existing commands. Reference seed:a3c8c5c8899dc619. The HTML concept is structural guidance, not a measured raster contract.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

Desktop design belongs in docs/desktop-design.md; root DESIGN.md and its sidecar remain the website's system. Verification uses synthetic data in a temporary native harness, never production credentials.
