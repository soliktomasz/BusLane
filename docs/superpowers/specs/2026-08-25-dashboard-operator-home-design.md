# Dashboard Operator Home Design

## Status

Approved in conversation on 2026-08-25. Implementation is not part of this document.

## Context

BusLane currently presents the namespace dashboard as a full-window feature overlay. Dashboard inbox actions select an entity, choose Active, Dead Letters, or Sessions, and load data in the underlying workspace. The overlay remains visible, so users cannot see the destination they opened.

This is more than a missing close command. Dashboard and entity operations compete for the same surface while using different navigation models. Daily Service Bus work needs one predictable hierarchy: namespace overview, entity, message view.

The existing dashboard also gives every ranked entity a row, including healthy entities with score zero. Review state records a snapshot but does not remove the reviewed item from priority work. Charts and summary metrics consume substantial space before users reach operational work.

This design changes dashboard from a feature overlay into the home of each connected namespace. It supersedes the earlier statement in `2026-07-07-buslane-warm-command-workspace-design.md` that dashboard should not be the default center of gravity. That visual design remains valid elsewhere. For a connected namespace, operator home is now the default entry point.

## Goals

- Make every dashboard action navigate to a visible destination.
- Optimize first screen for daily triage across many queues, topics, and subscriptions.
- Keep one namespace per connection tab and avoid entity-tab overload.
- Make location and return path obvious.
- Preserve existing entity, message, dead-letter, and session workflows.
- Keep large entity collections searchable, virtualized, and keyboard accessible.
- Show useful data during refresh, partial failure, and empty states.

## Non-goals

- Global message-content search across every entity.
- A new multi-document tab system for individual queues or subscriptions.
- Replacing existing message inspection, bulk operations, or session tools.
- Rebuilding dashboard persistence or custom widget editing.
- Automatically refreshing every background namespace tab.
- New Service Bus administration features unrelated to overview navigation.

## Operator Mental Model

Each connection tab represents one Service Bus namespace. Within that tab, users move between two workspace modes:

1. **Overview**: triage, entity search, namespace health, recent work, and analytics.
2. **Entity workspace**: topic subscriptions, active messages, dead letters, or sessions.

The namespace tab remains stable while content changes inside it. A normal click never creates another connection tab. A future explicit context action may open an entity in another tab for comparison, but it is not part of this implementation.

## Navigation Model

### Namespace entry

- First successful connection opens Overview.
- Each connection tab remembers whether its last workspace mode was Overview or Entity workspace.
- Switching connection tabs restores that mode.
- Dashboard refresh runs only when active tab is showing Overview.

### Entity navigation

Dashboard, search, pinned entities, recent entities, and entity tree use one navigation flow. Requests identify destination by meaning rather than numeric tab index.

Proposed destination values:

- `ActiveMessages`
- `DeadLetters`
- `Sessions`
- `TopicSubscriptions`

Queue and subscription requests can target Active Messages, Dead Letters, or Sessions when supported. Topic requests target Topic Subscriptions because messages are received from subscriptions, not directly from a topic.

Navigation changes visible workspace before starting network work. Users immediately see destination header and matching loading state. Load errors appear inside destination with Retry and Back to Overview actions.

### Breadcrumb and back behavior

Entity workspace shows a compact path such as:

`Overview / orders-eu / Dead letters`

For a subscription:

`Overview / payments / fraud-indexer / Active messages`

- Overview returns to home without discarding current overview data, search query, filters, or scroll position for that visit.
- Standard back navigation returns from Entity workspace to previous Overview section.
- Topic and subscription paths display full context to disambiguate repeated subscription names.

## Overview Information Architecture

The approved layout is **Triage home**.

### Header and entity search

Top area contains namespace identity, last successful refresh time, lightweight refresh status, manual Refresh, and entity search.

Entity search covers:

- Queues
- Topics
- Subscriptions

Results use fuzzy matching and group by entity type. Subscription results always show `topic / subscription`. Search works from loaded namespace inventory and does not perform network calls per keystroke.

Enter on a queue or subscription opens Active Messages. Enter on a topic opens Topic Subscriptions. Dead Letters and Sessions remain explicit actions so search never guesses a destructive or specialized work mode.

Global message-content search is excluded. Service Bus does not provide a reliable namespace-wide message query. Simulating it would require peeking many entities and would be slow, expensive, and incomplete. Message filtering remains inside selected entity workspace.

Keyboard behavior:

- `/` focuses search when focus is not inside an editor.
- Up and Down move through results.
- Enter opens selected result.
- Escape clears results first, then leaves search.

### Compact health strip

Health strip contains three namespace-level values:

- Needs action
- Total dead letters
- Active messages

Cards remain compact and secondary to priority work. Needs Action opens All Issues. Total Dead Letters opens All Issues filtered to dead-letter problems. Active Messages opens entity inventory sorted by active count.

### Priority work

Priority work shows five to eight actionable entities. Healthy score-zero entities never appear.

Each row shows:

- Queue name or full `topic / subscription` path.
- Entity type.
- Plain-language reason for ranking.
- Current count and change since review.
- One always-visible contextual action: Open DLQ, Open Sessions, or Open Messages.
- Secondary menu: Mark Reviewed, Pin or Unpin, Copy Name.

Critical actions are never hover-only. Hover may add emphasis but cannot control discoverability or layout.

`View all issues` opens the full virtualized issue list with severity, type, and reviewed-state filters.

### Continue work

Continue Work contains small Pinned and Recent groups. Entries display full path where needed and navigate through the same typed request flow as search and priority work.

Recent history records successful entity navigation only. Failed or cancelled navigation does not become recent work.

### Analytics

Charts move from home grid into a secondary Analytics section under Overview. Home may link to Analytics but does not render full charts.

Analytics retains:

- Active messages over time
- Dead letters over time
- Scheduled messages over time
- Total size over time
- Existing time-range controls

Charts use selected time range as viewport, keep zero baseline where meaningful, and show Collecting History until at least two samples exist.

## Review Semantics

Mark Reviewed acknowledges the current entity snapshot and removes that entity from Priority Work immediately.

Reviewed entity returns to Priority Work when any of these occurs:

- Dead-letter count increases above reviewed snapshot.
- Active backlog increases above reviewed snapshot.
- New unacknowledged alert appears.

Reviewed entities remain visible in All Issues under Reviewed filter. An unchanged known backlog does not repeatedly occupy top priority. Acknowledging an inbox item does not acknowledge an alert; alert acknowledgement remains separate.

Review identity is namespace plus full entity path. Subscription identity includes topic name, preventing collisions between subscriptions with same name on different topics.

## Loading, Refresh, Empty, and Error States

### Initial load

- Render section-shaped skeletons for health, priority, and continue-work regions.
- Keep namespace identity and entity search shell visible.
- Avoid a full-window loading overlay for dashboard data.

### Refresh

- Keep last successful snapshot visible.
- Show Updating near timestamp.
- Update sections progressively without resizing main layout.
- Ignore stale callbacks from previous namespace or refresh generation.
- Never mix histories or entity data between namespaces.

### Partial failure

- Keep successful sections interactive.
- Show contextual error inside failed section.
- Provide section-level Retry where operation can be retried independently.
- Preserve last valid data and label it with its timestamp.

### Empty state

When nothing needs attention, Priority Work shows `No issues need attention.` Search, Pinned, Recent, and Analytics remain available. Empty does not mean disconnected or uninitialized.

### Stale entity navigation

If entity disappears between refresh and action, destination shows that entity is no longer available and offers Refresh Overview and Back to Overview. It must not fail behind dashboard or silently select another entity.

## Large Namespace Behavior

- Search results and All Issues use virtualized controls rather than `ItemsControl` rendering every row.
- Priority Work remains capped at eight visible rows.
- Search computes from in-memory inventory and avoids remote calls during typing.
- Long names trim visually but expose full value through tooltip and Copy Name.
- Subscription results always retain topic context.
- Background namespace tabs do not auto-refresh dashboard data.

## Architecture

### Workspace ownership

Add a workspace mode to `ConnectionTabViewModel` so mode belongs to namespace tab rather than global feature panels.

Conceptually:

- `NamespaceWorkspaceMode.Overview`
- `NamespaceWorkspaceMode.Entity`

Main content selects Overview or existing entity/message workspace from active tab mode. Dashboard is removed from `FeaturePanelsViewModel.ShowCharts` and from full-window overlay stack. Other tools remain feature panels unless redesigned separately.

### Navigation requests

Replace inbox action callbacks that pass numeric selected-tab indexes with a typed request containing:

- Entity type
- Entity name
- Topic name when applicable
- Destination view

Main window remains navigation coordinator. It resolves current active tab, selects entity, changes workspace mode, and delegates loading to existing `MessageOperationsViewModel` or `SessionInspectorViewModel`.

The visible workspace changes before awaiting data. Destination loading and error state belong to destination view model.

### Overview state

`NamespaceDashboardViewModel` remains overview data and presentation coordinator for active namespace. Overview view stays mounted while hidden by Entity mode so same-tab return preserves current state. On active connection-tab change, coordinator receives new operations and namespace identity, clears incompatible chart/entity history, and refreshes only if new tab is in Overview mode.

Connection tab owns last workspace mode and previous Overview section. This is enough to restore tab location without introducing a new application-wide navigation framework.

### Priority collections

Separate priority and full issue projections:

- Priority projection filters actionable, unreviewed items and caps visible result.
- Full issue projection includes actionable and reviewed items for filtering.
- Search inventory is independent from issue ranking, so healthy entities remain discoverable.

Mark Reviewed persists snapshot, updates projections immediately, and does not wait for next namespace refresh.

### Existing components retained

- Entity trees
- Messages panel
- Active and dead-letter message loading
- Session inspector
- Metrics history store
- Alert service
- Pin store
- Existing connection tabs

## Accessibility and Interaction

- All primary row actions remain visible without hover.
- Every action has keyboard focus state and descriptive automation name.
- Search and issue lists support keyboard navigation.
- Breadcrumb links are focusable and ordered before page actions.
- Loading announcements use non-blocking status text.
- Error text includes recovery action and does not rely on color alone.
- Danger, warning, and neutral states retain contrast in light and dark themes.
- Minimum 1000 by 600 window keeps search, one priority row, and primary action visible without horizontal scrolling.

## Testing Strategy

### View model tests

- New connection tab defaults to Overview after successful connection.
- Each tab preserves its own workspace mode.
- Dashboard action changes mode before asynchronous load completes.
- Typed destinations select Active, Dead Letters, Sessions, or Topic Subscriptions correctly.
- Topic never attempts direct message loading.
- Namespace switch clears incompatible dashboard context and stale callbacks.
- Priority excludes healthy score-zero entities.
- Priority is capped at configured visible limit.
- Mark Reviewed removes item immediately.
- Reviewed item returns only after qualifying change or new alert.
- Review identity distinguishes same-named subscriptions on different topics.
- Search groups entity types and retains full subscription path.
- Recent work records successful navigation only.

### View contract tests

- Dashboard is normal workspace content, not feature overlay.
- Critical actions are present and not hover-only.
- Overview and Entity content bind to active tab workspace mode.
- Search and issue surfaces use virtualized list controls.
- Breadcrumb exposes Overview and destination labels.
- Loading, empty, stale, and error states are present.

### Integration and manual checks

- Open queue messages from Priority Work.
- Open queue DLQ from Priority Work.
- Open subscription DLQ with full topic context.
- Open sessions for session-enabled queue and subscription.
- Open topic and select subscription.
- Return to Overview and verify search, filters, and scroll remain.
- Switch between two namespace tabs in different modes.
- Test hundreds of queues and subscriptions for input and scroll responsiveness.
- Verify keyboard-only flow from Overview search to destination and back.
- Verify light and dark themes at minimum window size.

Build and full test suite remain required before completion.

## Migration Sequence

1. Add typed workspace mode and navigation destination with tests.
2. Move dashboard from feature overlay into normal namespace content.
3. Route inbox actions through typed visible navigation.
4. Add Overview breadcrumb and return behavior.
5. Build entity search and Continue Work using existing inventories and pins.
6. Split priority and full issue projections; implement review suppression.
7. Restructure home into approved Triage layout.
8. Move full charts into Analytics section.
9. Add loading, partial failure, empty, stale, and large-list states.
10. Run accessibility, theme, scale, and workflow verification.

Each stage should preserve existing message operations and compile before proceeding.

## Risks and Mitigations

### Global overlay assumptions

Some commands may assume feature panels cover base workspace. Dashboard must be removed surgically without changing lifecycle of Live Stream, Alerts, Correlation Explorer, or Scheduled Messages.

### Asynchronous navigation race

Fast entity changes can allow old loads to finish after newer navigation. Typed navigation should reuse or extend existing cancellation and stale-generation protections.

### Expensive namespace refresh

Large topic collections can make full subscription refresh slow. Keep last snapshot visible, update progressively, and avoid background-tab refresh.

### Review suppression hiding unresolved work

Reviewed items remain in All Issues, retain timestamp, and reappear on worsening data or new alerts. This keeps known backlog accessible without permanently occupying top priority.

### Shared dashboard coordinator

Overview data coordinator is active-namespace scoped. Connection tab owns navigation mode, while namespace switch explicitly resets incompatible data. Avoid duplicating auto-refresh loops per tab.

## Acceptance Criteria

- Dashboard action never leaves destination hidden behind overview.
- Connected namespace opens in Overview by default.
- Normal entity action reuses current namespace tab.
- Overview search finds queues, topics, and subscriptions with full paths.
- Priority Work contains only actionable unreviewed entities and shows at most eight.
- Mark Reviewed removes unchanged entity until qualifying change.
- Topics route to subscription browsing, not direct messages.
- Breadcrumb and back return to Overview predictably.
- Full charts are secondary under Analytics.
- Refresh preserves last valid data and localizes failures.
- Large issue/search collections remain responsive and keyboard accessible.
- Existing active, DLQ, session, and message operations remain behaviorally intact.
