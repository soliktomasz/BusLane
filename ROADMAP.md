# BusLane Roadmap

This roadmap reflects the current BusLane codebase and planned feature work as of February 17, 2026.

> Have a feature request? Open an [issue](https://github.com/soliktomasz/BusLane/issues) or start a [discussion](https://github.com/soliktomasz/BusLane/discussions).

## Current Baseline (Already in Product)

- Multi-connection tab workflow with session restore
- Queue/topic/subscription browsing with DLQ support
- Message operations: peek, send, filter/search, bulk actions, export/import
- Live stream, charts, alerts, and namespace dashboard
- Connection library with encrypted storage
- Keyboard shortcuts and improved desktop UX (including app menu/About)
- Update checking/downloading infrastructure and cross-platform packaging groundwork

## 2026 Feature Roadmap

## Horizon 1: Message Workflow Excellence (Q1 2026)

### 1. Message Templates v2
- [ ] Parameterized templates (tokens like `{{OrderId}}`)
- [ ] Template categories/tags and quick search
- [ ] Duplicate/edit from existing message payload

### 2. Safer Bulk Operations
- [ ] Dry-run preview for bulk delete/resend actions
- [ ] Progress + partial-failure summary UI
- [ ] Optional operation confirmation rules for destructive actions

### 3. Session-Aware Operations
- [ ] Better tools for session-enabled queues (session drill-down and diagnostics)
- [ ] Persist last-used session filters per tab

## Horizon 2: Monitoring & Incident Response (Q2 2026)

### 4. Historical Metrics
- [ ] Persist metric snapshots locally with configurable retention
- [ ] Time-range comparisons (last hour/day/week)
- [ ] Trend overlays for queue depth and DLQ growth

### 5. Dashboard Presets
- [ ] Save/load dashboard presets per namespace
- [ ] One-click reset to default layout
- [ ] Import/export dashboard configuration

### 6. Alerting Improvements
- [ ] Quiet hours and rule cooldown windows
- [ ] Better rule testing experience and alert history panel
- [ ] Optional notification channels beyond in-app toast (pluggable)

## Horizon 3: 1.0 Readiness (Q3 2026)

### 7. Distribution & Trust
- [ ] Production-ready installers/signing pipeline
- [ ] macOS notarization, Windows installer polish, Linux package validation
- [ ] Release quality gates and smoke-check checklist

### 8. Reliability & Performance
- [ ] Performance pass for large message/entity sets
- [ ] Better reconnection handling for intermittent network/auth failures
- [ ] Memory usage guardrails for long-running live stream sessions

### 9. Documentation & Onboarding
- [ ] End-to-end user guide with common workflows
- [ ] Troubleshooting playbooks (auth, permissions, connectivity)
- [ ] Feature walkthroughs for dashboards, alerts, and bulk operations

## Future / Discovery Backlog

These are intentionally unscheduled until core 1.0 goals are complete.

- [ ] Azure Event Hubs support
- [ ] Azure Storage Queues support
- [ ] Message scheduling calendar and replay tooling
- [ ] Shared team profiles/connection bundles (secure)
- [ ] AI-assisted search/triage helpers

## Prioritization Rules

When tradeoffs are needed, prioritize work in this order:

1. Reliability and data safety
2. High-frequency operator workflows
3. Monitoring signal quality
4. Platform distribution polish
5. Experimental features

## Definition of Done for Roadmap Items

Each item is considered complete only when:

- [ ] Unit/integration tests are added or updated
- [ ] UI behavior is validated on macOS, Windows, and Linux paths where applicable
- [ ] User-facing docs are updated
- [ ] No secrets or sensitive data are introduced in logs/telemetry

---

This roadmap is a living document and will be updated as priorities evolve.
