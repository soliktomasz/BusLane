---
version: 1
slug: "desktop-activity-entity-details"
primary_target: "BusLane/Views/Controls/LogViewerPanel.axaml"
related_targets: ["BusLane/Views/Dialogs/EntityDetailDialog.axaml", "BusLane/Views/Controls/LogViewerPanel.axaml.cs"]
---

# Desktop Activity Log and entity details

## Intent and authority

Mode: Operate. The user confirmed “Keep the drawer and compact details dialog.” This bounded redesign extends the incumbent [desktop design system](../../docs/desktop-design.md): native Avalonia/Fluent controls, Inter, Lucide icons, violet feedback, and dynamic light/dark resources. Root PRODUCT.md and DESIGN.md describe the website; they do not govern these desktop controls. No identity replacement, global token change, or sidecar is introduced.

## Implemented composition

Activity Log remains a right-hand drawer (600px), capped to the available control width. Its overlay dismisses the drawer. A persistent header contains the title (20px, semibold), Copy visible, and an accessible Close icon button (34 × 34px). A full-width search field precedes labeled Level and Source selectors, each retaining a minimum height (36px). A fixed count strip identifies the visible and total entries and their newest-first order.

Entries use a native virtualized ListBox, flat row surfaces, thin bottom dividers, and tonal hover feedback. Row padding is (24px horizontal, 16px vertical). Timestamp, level, and source share a compact metadata line (12px); timestamps show milliseconds and expose the full date through a tooltip. Wrapping messages use (14px, medium); optional selectable details use (13px). Each entry retains its copy icon and existing context-menu commands. Severity keeps text labels and dynamic semantic foregrounds; Warning uses TextWarningHover for the stronger existing warning treatment.

The persistent footer retains Auto-scroll and Debug logs as native shared pill toggles and Clear all as a destructive text action. Copy visible is disabled with no visible entries; Clear all is disabled with no stored entries. Auto-scroll keeps the newest entries in view when enabled. The control observes view-model and collection changes, coalesces scrolling with the native ScrollViewer, and removes its subscriptions on detach. Disabling Auto-scroll allows reviewing an older position; enabling it returns to the newest entries.

Entity details uses the existing native modal with maximum dimensions (720 × 680px), an outer margin (24px), and no inner modal padding. A persistent title (20px, semibold), supporting description, and Close icon precede fixed Property/Value headings. Property rows scroll independently, using a property column (180px), a gap (24px), row padding (12px horizontal, 14px vertical), and thin dividers. Labels (13px) and selectable values (14px) wrap. The fixed footer explains read-only selection/copying and offers Close.

## States and preservation

Activity Log distinguishes No activity yet from No matching entries. The former explains when entries appear; the latter points to search, level, source, and Debug logs. Search covers messages and details. Preserve level/source filtering, debug inclusion, entry copying, newest-first order, and existing command bindings. Copy visible copies filtered entries; Clear all clears stored logs, including hidden entries. Native controls retain their keyboard behavior, and icon actions and search/filter controls expose accessible names or labels.

Preserve the existing detail rows: queue identity, counts, size, session requirement, TTL, lock duration, and accessed time; topic identity, subscription count, size, TTL, and accessed time; subscription identity/topic, counts, session requirement, lock duration, delivery limit, TTL, idle deletion, forwarding, and accessed time. Queue and topic size labels explicitly read Size (bytes). Value formatting and Service Bus operations remain intact. The related search fix clears the disposed debounce cancellation-source reference so clearing search and then searching again can safely schedule a new filter.

## Verification and limits

Source evidence: `BusLane/Views/Controls/LogViewerPanel.axaml`, `BusLane/Views/Controls/LogViewerPanel.axaml.cs`, `BusLane/Views/Dialogs/EntityDetailDialog.axaml`, `BusLane/ViewModels/LogViewerViewModel.cs`, `BusLane/ViewModels/Core/EntityOperationsViewModel.cs`, shared styles in `BusLane/Styles/AppStyles.axaml`, and theme resources in `BusLane/App.axaml`.

The [native capture matrix](../review/activity-entity-details/) contains 32 synthetic Avalonia captures: activity, activity with Debug enabled, filtered activity, no matching activity, an empty activity library, queue details, topic details, and subscription details. Each state is captured at window sizes (1280 × 800px) and (1000 × 600px), in light and dark themes. The grey backdrop is a synthetic application backdrop. These captures establish rendering and layout at those sizes; they do not establish real Azure activity, clipboard integration, assistive-technology output, or Windows/Linux rendering. This native Avalonia surface has no HTML/CSS detector result.

The initial independent review inspected 28 native captures and requested stronger light-theme warning contrast, stronger dark-theme checked-toggle contrast, and explicit size units. The implementation uses the existing TextWarningHover resource, shared pill toggle styling, and Size (bytes) labels for those revisions. The final review inspected eight updated images, including all four empty-library captures, marked all three findings resolved, and returned a ship disposition at the reviewed scope. No source edits followed the final captures.

The final full test suite reports 906 passed, one skipped, and four pre-existing website failures (911 total). The failures are in GitHubPagesLandingPageTests: LandingPage_ScreenshotGallery_UsesAccessibleLocalTabs, LandingPage_ApprovedHero_UsesProductLedMessage, LandingPage_DisplayPreferences_ProvideDarkAndReducedMotionModes, and LandingPage_VisibleCopy_ContainsNoBannedDashCharacters. These review and test results were supplied by the implementation workflow, rather than inferred from the images.

The final native harness reported AUTO_SCROLL_CHECK_PASSED: disabling Auto-scroll preserved an older position, enabling it returned to the newest entry, and incoming entries followed while enabled. The disposed search-source regression was reproduced by a failing test before the fix. These checks use synthetic data and do not establish production Service Bus behavior.
