# BusLane Roadmap

This roadmap reflects the current BusLane codebase and planned feature work as of July 1, 2026.

Progress audit: the February 2026 roadmap was stale. Several Q1/Q2 items have since shipped and are marked below. Items left unchecked still need product/UI hardening, documentation, platform validation, or broader implementation.

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
- [x] Parameterized templates (tokens like `{{OrderId}}`)
- [x] Template categories/tags and quick search
- [x] Duplicate/edit from existing message payload

### 2. Safer Bulk Operations
- [x] Dry-run preview for bulk delete/resend actions
- [x] Progress + partial-failure summary UI
- [x] Optional operation confirmation rules for destructive actions

### 3. Session-Aware Operations
- [x] Better tools for session-enabled queues (session drill-down and diagnostics)
- [x] Persist last-used session filters per tab

## Horizon 2: Monitoring & Incident Response (Q2 2026)

### 4. Historical Metrics
- [x] Persist metric snapshots locally with configurable retention
- [x] Time-range comparisons (last hour/day/week)
- [x] Trend overlays for queue depth and DLQ growth

### 5. Dashboard Presets
- [x] Save/load dashboard presets per namespace
- [ ] One-click reset to default layout
- [ ] Import/export dashboard configuration

### 6. Alerting Improvements
- [x] Quiet hours and rule cooldown windows
- [x] Better rule testing experience and alert history panel
- [x] Optional notification channels beyond in-app toast (pluggable)

## Horizon 3: 1.0 Readiness (Q3 2026)

### 7. Distribution & Trust
- [x] Production-ready release packaging and Velopack update pipeline
- [ ] macOS notarization, Windows installer polish, Linux package validation
- [ ] Release quality gates and smoke-check checklist

### 8. Reliability & Performance
- [x] Performance pass for large message/entity sets
- [ ] Better reconnection handling for intermittent network/auth failures
- [x] Memory usage guardrails for long-running live stream sessions

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
