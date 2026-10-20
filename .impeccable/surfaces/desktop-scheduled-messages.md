---
version: 1
slug: "desktop-scheduled-messages"
primary_target: "BusLane/Views/Controls/ScheduledMessagesView.axaml"
---

# Desktop Scheduled messages console

## Intent and authority

Mode: Operate. This bounded redesign extends the incumbent [desktop design system](../../docs/desktop-design.md) and reuses the Namespace overview and Alerts vocabulary. Root PRODUCT.md and DESIGN.md describe the website and do not govern this screen. The previous look (stacked filter cards, five fixed-width labelled combos, card rows, a plain-button calendar, status text drawn over content) is replaced.

## Implemented composition

- Header (in `MainWindow.axaml`): violet CalendarClock tile (40px), "Scheduled messages" (20px semibold) over a summary ("N upcoming · M in the local index", "· Refreshing" while loading), outlined Refresh and icon-only Close (34px).
- List and Calendar are `section-tab` radio tabs.
- One flat filter bar: search (440px, search icon) with a permanent "Local index, not verified with the broker" note and full tooltip; then wrapping inline-labelled filters (Status, When, Environment, Connection, Entity; min 116px), the shown count, and Clear filters.
- List: full-bleed table, Scheduled for 112 (local HH:mm over ddd d MMM yyyy) · Destination * (entity over connection · environment) · Message * (message ID or "No message ID" over "Seq N · body preview") · State 150 · actions 140. State is a tinted pill (Upcoming brand, Due/Limited warning, Rescheduled success, Action failed danger, others neutral) over "Broker confirmed" or "Local index"; the full local state is in the tooltip. Reschedule is a visible 28px row action; the More menu (`menu-flyout`, 280px) holds Clone, Resolve locally, and a separated destructive Cancel message, each with a one-line description.
- Empty states distinguish an empty index ("No scheduled messages yet") from filters with no matches (with Clear filters, noting Status starts on Upcoming).
- Calendar: previous/next icon buttons and the month title, a Mon–Sun header, a 7 × 6 grid of seamed cells (out-of-month cells on the subtle surface) with day number, count, and compact violet time-and-entity chips. The grid has a minimum height (540px) and scrolls on short windows. Choosing a chip opens a bottom detail strip with time, entity, state, connection, message ID, and Reschedule, Clone, Resolve locally, Cancel message, and Close.
- Status and error feedback render as infobars below the content.
- Confirmation modal (600px): action-specific title, destination/connection/time/sequence details, date and time pickers and the partial-failure explanation only for Reschedule, an irreversibility note only for Cancel, the Production acknowledgement only for Production entries, inline validation, and Back plus primary Reschedule or danger Cancel message, disabled until a required acknowledgement is given.

## Behavior changes

- Reschedule now prefills the current scheduled date and time; previously the pickers opened empty while Cancel (which ignores them) prefilled them.
- Opening a confirmation clears stale status text so validation shows inside the modal.
- New view-model members: `Summary`, `HasEntries`, `IsRescheduling`, `ConfirmationTitle`, `ConfirmationButtonText`, `RequiresProductionAcknowledgement`, `CanConfirm`, and `ClearFiltersCommand` (resets search and every filter to All).
- Calendar selection is now visible and actionable; previously `SelectedEntry` had no display.

## Verification and limits

The [native capture matrix](../review/scheduled-messages/) covers 28 headless renders of the view (all-states list, Upcoming list, empty index, no matches, calendar with selection, Production cancel confirmation, reschedule confirmation) at content sizes for (1280 × 800px) and (1000 × 600px) windows, in both themes, using synthetic entries and a mocked service. The header lives in MainWindow and was verified by build and source review, not capture. The full suite reports 917 passed, 1 skipped, and the 4 pre-existing GitHubPagesLandingPageTests failures; view contract tests were rewritten for the new structure, and two view-model tests cover the reschedule prefill and the Production gate.

Not verified: real broker cancel/reschedule, clone into the composer, flyout dismissal on item choice in a live window, Windows/Linux rendering, and theme switching while open (state brushes resolve at bind time). Reschedule remains offered for every state, as before.
