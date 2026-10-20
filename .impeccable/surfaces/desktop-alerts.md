---
version: 1
slug: "desktop-alerts"
primary_target: "BusLane/Views/Dialogs/AlertsDialog.axaml"
---

# Desktop Alerts dialog

## Intent and authority

Mode: Operate. This bounded redesign extends the incumbent [desktop design system](../../docs/desktop-design.md): native Avalonia/Fluent controls, Inter, Lucide icons, violet feedback, and dynamic light/dark resources. Root PRODUCT.md and DESIGN.md describe the website and do not govern this dialog. The previous look (fixed 800 × 650px modal, card-in-card sections, 40px icon tiles per row) is replaced by the Namespace overview's vocabulary: a kind-tile header, label-width section tabs, and flat timetable rows.

## Implemented composition

- The modal stretches within a (24px) margin, capped at (960 × 720px), minimum width (720px). The fixed header shows a violet Bell tile (40px), Alerts (20px, semibold), and a summary line (awaiting-acknowledgement count, enabled/total rules, or "No rules yet"), with an outlined icon-only Close (34px).
- Active, Rules, and History use the shared `section-tab` strip. Active and Rules show `counter-active` counts when non-zero.
- Each tab has a fixed toolbar (min 58px) with a one-line factual caption and its actions, above an independently scrolling list. Lists are full-bleed rows (min 56px, 24px inset, one seam, instant `HoverBackground` tint); there are no nested cards.
- Active: column header (Severity 84 · Alert * · Value 96 right-aligned · Triggered 92 · action 128, gaps 12px). Value shows the current figure over "limit N"; time shows local HH:mm:ss over dd MMM yyyy. Acknowledge is a 28px outlined row action; acknowledged rows drop to subtle foreground (not opacity) and show a success check. Toolbar: Clear acknowledged (outline) and Acknowledge all (primary, disabled when nothing awaits).
- Rules: enable toggle, name with severity pill and "Off" when disabled, and a wrapping facts line (metric ≥ threshold, pattern or All entities, cooldown, quiet hours, webhook) with 13px Lucide icons. Test (outlined, tooltip explains it adds a sample alert), Edit, and Delete (danger foreground) are always visible.
- Rule form replaces the list in place. Its toolbar holds Back, the form title, a "Name the rule to save it" hint, Cancel, and Save rule (disabled while the name is empty), so actions stay visible while the form scrolls. Groups: Condition (name; Metric, Alert at or above, Severity; Entity pattern in the monospace stack), Noise control (cooldown, quiet from/until with one explanatory hint), Delivery (webhook, Enabled). Form width is capped at (680px); fields share a (38px) minimum height.
- History: Time 92 · Outcome 116 · Rule * · Value 96. Outcome pills: Triggered warning, Delivered success, Delivery failed danger, Acknowledged brand, Suppressed neutral; suppressed rows append "during cooldown" / "during quiet hours". Details wrap beneath.
- The footer keeps the System notifications toggle.

## Behavior changes

- Editing a rule now loads its metric, severity, and threshold; previously the form kept stale values, so saving silently rewrote the rule's condition. Covered by `ShowEditRule_WithRule_LoadsTypeSeverityAndThreshold`.
- The rule-list toggle now calls `ToggleRuleEnabledCommand` (persisted through the alert service); previously it two-way bound to the record's init-only property and did not persist.
- Metric names use `AlertsViewModel.GetAlertTypeDisplayName` instead of raw enum names.

Avalonia `Run` inlines inside these item templates did not apply value converters; meta lines use `MultiBinding` or separate TextBlocks instead.

## Verification and limits

The [native capture matrix](../review/alerts/) covers 32 headless Avalonia renders: active, active-empty, rules, rules-empty, edit, add, history, and history-empty, at window sizes (1280 × 800px) and (1000 × 600px), in both themes, using synthetic rules, alerts, and history with a mocked alert service. The full test suite reports 915 passed, 1 skipped, and the 4 pre-existing GitHubPagesLandingPageTests failures. The scaffold test now asserts the shared section-tab strip instead of `dialog-body`.

Not verified: live alert evaluation, webhook delivery, desktop notifications, theme switching while the dialog is open (severity/outcome converters resolve brushes at bind time, like the existing severity converters), assistive-technology output, and Windows/Linux rendering. Delete still removes a rule without confirmation, as before.
