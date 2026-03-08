# Namespace Priority Inbox Design

**Date:** 2026-03-08
**Status:** Approved

## Overview

Add a single-namespace triage inbox for power users that turns BusLane's existing metrics, alerts, and message tooling into a ranked work queue. The inbox should help users answer three questions quickly: what needs attention now, what changed since I last checked, and how do I jump straight into investigation.

## Requirements

- Add a namespace-level inbox panel focused on queues and subscriptions within the current namespace.
- Rank inbox items by urgency using lightweight heuristics derived from current counts, recent metric history, and active alerts.
- Show "since last check" deltas per entity so unchanged entities can be ignored quickly.
- Let users open a preconfigured investigation flow from an inbox item with one click.
- Keep the implementation aligned with current BusLane architecture instead of introducing a second dashboard or a second message browser.

## Functionalities

### 1. Priority Inbox

The inbox is an entity-first list of queues and subscriptions that need attention. Each item represents one queue or one topic subscription and includes:

- entity name and type
- urgency score
- short reasons such as "DLQ growing", "backlog spike", or "2 active alerts"
- key counts for active, dead-letter, and scheduled messages where applicable

The inbox should favor explainable scoring rather than opaque ranking. Users need to understand why an item is near the top.

### 2. Since-Last-Check Deltas

Each inbox item shows how the entity changed since the last user review. At minimum the inbox should track:

- active message delta
- dead-letter delta
- alert count delta
- last observed activity time

Review state should be local to the app and per namespace/entity. Opening the entity from the inbox marks the current snapshot as reviewed without altering message data or alert state.

### 3. Inbox-to-Investigation Jump

Every inbox item should support direct actions that route into existing tools:

- open messages view
- open DLQ view
- open session inspector when sessions are required
- apply the correct entity selection and message tab state before loading data

The inbox is a launch surface, not a second investigation UI. Detailed inspection stays in `MessageOperationsViewModel` and `SessionInspectorViewModel`.

## Architecture

### State ownership

Add a dedicated `NamespaceInboxViewModel` that owns the triage list, scoring state, review timestamps, and row-level commands. `NamespaceDashboardViewModel` remains the place for summary charts and metric cards; it should not absorb inbox-specific behavior.

### Data sources

The inbox should build from the data BusLane already produces:

- current `QueueInfo` and `SubscriptionInfo` from namespace refreshes
- historical metric snapshots from `IMetricsHistoryStore`
- active alerts and alert history from `IAlertService`
- current selection and navigation state from `NavigationState`

This keeps the feature incremental and avoids a new polling or persistence stack.

### Scoring model

Use a deterministic heuristic score made of small weighted factors:

- current dead-letter count
- dead-letter growth compared to the previous window
- active backlog growth compared to the previous window
- scheduled buildup for queues
- active unacknowledged alerts
- recent activity recency

The first version should store both the numeric score and human-readable reasons so the UI can explain the ranking.

### Review state persistence

Persist review state locally by namespace and entity name. Each review record should store:

- namespace identifier
- entity key
- last reviewed timestamp
- snapshot values at review time

This supports delta calculations without changing the existing metrics history store contract.

## UI Changes

- Add a new `NamespaceInboxView` control rendered within the namespace dashboard experience.
- Place the inbox near the top of the namespace dashboard, before charts, because it is a triage surface rather than a retrospective analytics view.
- Each row should show priority, reasons, deltas, and quick actions in a compact card/list format.
- Add an empty state when no entities need attention and a loading state during refresh.

## Error Handling

- If historical data is missing, still show inbox rows using current metrics and alerts with neutral deltas.
- If review-state persistence fails, the inbox should remain usable and simply omit "since last check" comparisons for the affected entities.
- If alert or metrics services return partial data, scoring should degrade gracefully rather than hiding the inbox.

## Testing Strategy

- Add unit tests for scoring and ranking logic with controlled metric history and alert inputs.
- Add unit tests for review-state persistence and delta calculation.
- Add view model tests for investigation jump commands to verify they set the correct navigation state.
- Build after the new view and bindings are introduced to catch compiled-binding regressions.
