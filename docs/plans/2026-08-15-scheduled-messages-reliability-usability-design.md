# Scheduled Messages Reliability and Usability Design

## Problem

Two independent defects make the Scheduled Messages panel misleading:

1. `ScheduledMessageStore` persists JSON with camelCase property names but checks for `SchemaVersion` using PascalCase. Current schema records are therefore treated as legacy schema 1 during every read, classified as `Limited / stale`, and hidden by the default `Upcoming` filter.
2. The empty-state border shares a grid cell with list/calendar content. Showing the empty state does not hide the underlying calendar or list.

The filter toolbar also relies on placeholders instead of visible labels, and the list has no column headings or result summary. Users cannot confidently identify what each control changes or scan rows efficiently.

## Reliability Design

- Detect the persisted camelCase `schemaVersion` property when deciding whether a record needs legacy migration.
- Preserve the legacy migration path for records that genuinely lack a schema version.
- Keep the default `Upcoming` status filter. Once records retain schema 2, current future schedules are classified and displayed correctly.
- Hide the list/calendar content container whenever the filtered projection is empty, leaving the empty state as the only rendered content.

## Interface Design

Use the existing BusLane design tokens, Lucide icons, typography classes, and surfaces.

- Group search, List/Calendar controls, and Refresh into a compact command area.
- Put the five filters in a bordered filter surface with a `ListFilter` icon and visible labels: Connection, Entity, Environment, Status, and Time range.
- Use a wrapping layout so filter fields remain legible when the panel narrows.
- Give search and every filter an explicit accessibility name and predictable keyboard order.
- Add a results header with the filtered message count.
- Add list column headings aligned with the existing row grid: Scheduled, Connection, Entity, Message / status, and Actions.
- Replace the generic empty copy with `No scheduled messages match these filters` and guidance to adjust filters or refresh.

No new theme, bespoke controls, sidebar, animation system, or filter-reset command is introduced.

## Testing

- Add store regression tests proving a current camelCase schema record remains schema 2 and a schema-less legacy record migrates to schema 1.
- Extend the scheduled-messages view contract test to require visible filter labels, accessibility names, result/column headings, filter-aware empty copy, and an empty-state visibility gate around result content.
- Run focused tests, the full test suite, and a full build.
