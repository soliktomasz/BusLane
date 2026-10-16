---
name: "BusLane desktop workspace"
description: "Native Avalonia workspace for browsing entities and inspecting messages in context."
colors:
  brand-light: "#4338CA"
  brand-dark: "#B8ADFF"
  canvas-light: "#F6F5FA"
  canvas-dark: "#17171E"
  surface-light: "#FFFFFF"
  surface-dark: "#202028"
  sidebar-light: "#EEEBF6"
  sidebar-dark: "#17171E"
  foreground-light: "#242138"
  foreground-dark: "#EDECF3"
  subtle-light: "#69647F"
  subtle-dark: "#A1A1AA"
  border-light: "#E5E1ED"
  border-dark: "#35323F"
  selected-light: "#F0ECFF"
  selected-dark: "#343044"
  danger-light: "#E11D48"
  danger-dark: "#FB7185"
  success-light: "#16A34A"
  success-dark: "#4ADE80"
  warning-light: "#D97706"
  warning-dark: "#FBBF24"
typography:
  body:
    fontFamily: "Inter, Segoe UI Variable, SF Pro Display, Segoe UI, San Francisco, Helvetica Neue, Arial"
    fontSize: "14px"
    lineHeight: 20
  workspace-title:
    fontSize: "20px"
    fontWeight: 600
  row-subject:
    fontSize: "13px"
    fontWeight: 600
  caption:
    fontSize: "12px"
    lineHeight: 16
  preview:
    fontFamily: "Cascadia Code, Consolas, Monaco, monospace"
    fontSize: "12px"
  payload:
    fontFamily: "Cascadia Code, Consolas, Monaco, monospace"
    fontSize: "13px"
rounded:
  small: "6px"
  medium: "8px"
  large: "12px"
components:
  message-row:
    rounded: "0px"
    padding: "10px 16px"
  workspace-pane:
    rounded: "0px"
    padding: "0px"
  message-command-bar:
    rounded: "0px"
    padding: "12px 16px"
  payload:
    typography: "{typography.payload}"
    padding: "12px"
---

# Design System: BusLane desktop workspace

## Overview

This record covers the native Avalonia desktop UI. Root [DESIGN.md](../DESIGN.md), [PRODUCT.md](../PRODUCT.md), and [.impeccable/design.json](../.impeccable/design.json) describe the website; their palette and type choices do not govern desktop controls. Runtime resources in `BusLane/App.axaml` and native styles remain the implementation source of truth; the frontmatter records their observed desktop values.

The focused desktop workspace keeps entity navigation, the message table, and a read-only payload inspector visible together. It implements the approved light-concept arrangement in both light and dark themes: violet navigation, pale lavender light surfaces, graphite dark surfaces, Inter controls, Lucide icons, and thin seams. Existing connection, session, locked settlement, and bulk workflows remain available.

The workspace direction is recorded in [Focused desktop workspace](../.impeccable/surfaces/buslane-views-controls-messagespanelview-axaml.md). The concept supplies structural guidance; native Avalonia controls and actual commands define the shipped behavior.

## Colors

### Primary

Brand violet marks the primary Send action, selected navigation, active tabs, and splitter hover/focus. Light and dark variants use the frontmatter's respective brand tokens; the dark primary action uses a dark `TextOnBrand` foreground, while the light action uses white. Selected surfaces use a quiet violet tint rather than a filled primary action treatment.

### Neutral

Canvas, card, sidebar, foreground, subtle text, and border tokens describe the layers of each theme. Adjacent workspace panes are distinguished by surface tones and seams. Body and line-number foregrounds also follow the active theme.

### Semantic

Danger identifies dead-letter diagnostics and destructive operations; success and warning retain their existing operational meanings. Semantic foregrounds and tinted surfaces come from the theme dictionaries rather than locally fixed colors.

Use dynamic resources for theme-dependent UI. Entity-selection bindings also observe `Window.ActualThemeVariant`, so changing theme refreshes selected queue, topic, and subscription presentation. Placeholders use `SubtleForeground` with `TextControlPlaceholderOpacity` (1); do not compound that deliberately subtle foreground with additional opacity.

## Typography

Inter with the existing platform fallbacks remains the UI family. The shared desktop body and caption styles use the frontmatter roles. Workspace entity titles use the larger workspace-title role; table subjects and tab titles use compact semibold text. Message IDs and timestamps use the UI family. Code, payloads, and optional row body previews use the existing monospace stack.

Rows show subject above sequence number, message ID in its own column, and enqueue time above its date (`HH:mm:ss` and `dd MMM yyyy`). Keep the year and full message-ID tooltip. Trimming supports scanning; selectable inspector metadata and explicit full details provide the complete values.

## Layout

The window starts at (1440 × 900) with a minimum of (1000 × 600). New preferences default to the collapsed tool rail (56px); saved navigation preferences are preserved. Expanding navigation uses the existing sidebar (256px). Namespace tabs occupy a compact top strip (44px), with horizontal scrolling, status, close controls, and New connection access; the repeated tab subtitle is hidden.

Azure Identity and connection-string workspaces share a resizable entity/message split. The entity pane starts at (240px), with minimum (220px), a native splitter (5px), and a message-workspace minimum (420px). Hiding the entity pane releases its width; showing it restores the previous entity/message widths for that workspace during the window lifetime.

Within the message workspace, a flexible table sits beside the inspector (360px), separated by a native splitter (5px). The two columns each have a minimum of (300px) while side by side. Below (660px) of available message-workspace width, the table and inspector stack vertically with a horizontal splitter when available height is at least (640px). When the pane is both narrower and shorter than those thresholds, the inline inspector hides so the table retains its full height; every row still offers full details. These thresholds follow actual available workspace dimensions, including changes caused by expanded navigation. When the inspector is hidden, its column and splitter release their space.

The inspector is hidden while the Sessions tab is selected, locked messages are present, or multi-select mode is active. Those workflows reclaim the full message-workspace width. At inspector heights below (460px), compact mode reduces header padding and hides the repeated subject, ID, peek label, and summary block to preserve payload space. Body/Properties tabs, copy, expand, clear, dead-letter diagnostics, and footer actions remain available.

A full-width entity header spans the table and inspector: a (40px) violet-tinted kind tile (queue, topic, or subscription icon), the entity title (20px semibold), and a `Kind / namespace` line (13px) sit opposite outlined Refresh, primary Send message, and an outlined More overflow, all (34px) tall with the small radius. Below (760px) of panel width Refresh collapses to its icon so the title keeps its room. Under the Active / Dead letter tabs, one shared search row holds the search field scoped to loaded messages and the Select toggle (both 34px), followed by bulk actions in Select mode; clear search uses (30px). Session context can wrap; its label is capped at (180px), trims, and exposes the full value through a tooltip.

Message headers and rows align two flexible subject/ID columns, an enqueue column (92px), and a details-action column (28px), with column gaps (12px). Active and dead-letter lists remain virtualized. Row padding is defined in the frontmatter; optional body preview is one trimmed line.

## Elevation & Depth

The workspace is flat: square pane surfaces, thin dividers, tonal hover/selection feedback, and no row shadows. A bottom divider (1px) separates rows. The native splitters use muted seams at rest and violet on hover or keyboard focus. Existing dialogs, loading overlays, and other desktop surfaces retain their established elevation; this workspace treatment does not redefine them.

## Motion

Motion is feedback and continuity, never decoration. Button, tab, and splitter tints ease over (120-150ms); message rows change hover and selection tint instantly, using the translucent `HoverBackground` overlay so hover stays visible in both themes. Main-screen command buttons settle from a (0.96) press scale. Selecting a message settles the inspector content from (0.35) opacity and a (4px) offset over (180ms) with `cubic-bezier(0.16, 1, 0.3, 1)`; a newer selection interrupts the running one. Message empty and loading states fade in over (200ms). The selected tab's bar grows from the centre over (220ms), and switching tabs crossfades the content over (150ms). Avalonia exposes no reduced-motion preference, so every effect stays short, small, and non-spatial beyond a few pixels.

## Shapes

Shared desktop radii remain small controls, buttons, and cards/dialogs as recorded in the frontmatter. Workspace panes and message rows have square corners; search keeps the small radius. Namespace tabs retain softly rounded top corners (6px); they sit on the strip's bottom seam, stay transparent at rest, and the active tab joins the content surface with a (2px) violet cap instead of a full outline. Search, Select, Refresh, and More share the small radius and a thin border. The Active/Dead Letter and Body/Properties strips share one tab treatment: Medium-weight labels that change only colour, a label-width (2px) violet bar on a full-width seam, a soft hover/focus pill, and a muted Active count while that tab is not showing. Existing rounded badges and state indicators remain intact.

## Components

### Entity navigation

Retain Azure Identity and connection-string trees, search, entity commands, hide/show, and selected-state feedback. The native splitter remains keyboard focusable with violet hover/focus feedback. The entity-pane restore command remains beside the workspace title. The collapsed rail and expanded sidebar expose the existing navigation and tool workflows.

### Message table and commands

Selecting a row sets the inspected message without opening a modal. Both active and dead-letter rows provide an explicit full-details button at rest, including while the inline inspector is hidden. Row context menus expose Resend, Clone, Export, and Copy body. Multi-select mode adds checkboxes and preserves its selection and bulk actions.

The More overflow is a card-surface menu (296px, 10px radius, soft shadow) with three groups: Sort by enqueued time as Newest first / Oldest first with a violet check on the current order; Receive locked and Receive deferred, each with a one-line factual description, the deferred sequence-number field and its Receive action always visible; and a separated Purge messages item in danger red that notes its preview step. Choosing an action closes the menu. Keep Send message prominent. Preserve search/filter, sort, refresh, pagination, empty/loading/error states, and active/dead-letter navigation. More retains receive-locked and deferred receive, including sequence input and its command. Session selection, session scope, locked-message settlement, bulk resend/export/delete, and dead-letter resubmit retain their existing visibility and command conditions. Visual changes do not change Service Bus semantics.

### Inline message inspector

The empty inspector prompts for message selection and explains that peeking leaves messages in the queue. With selection, the header identifies read-only message details and provides explicit expand and clear actions. Body shows content type, Copy body, a read-only syntax-highlighted payload with line numbers, and a metadata summary. Properties scrolls independently and includes enqueue/expiry, optional session/scheduling/dead-letter source, application properties, and access to all message properties.

Dead-letter reason and error description occupy a tinted danger area above the tabs; long diagnostics scroll within (100px). Resend, Export, and Clone remain in the persistent footer. The payload follows dynamic surface/text resources; JSON and XML highlighting select separate light and dark definitions and refresh when the theme changes. Plain text remains readable without syntax coloring.

Full message details are opt-in through a row, the inspector expand action, or Open all message properties. A changed selection collapses expanded details. Escape first dismisses higher-priority overlays, then collapses expanded details while preserving selection; a subsequent Escape clears the inspected message.

### Expanded message details

Message details uses the approved Split inspector layout. A fixed 260px metadata rail shows identity, timing, delivery, and conditional routing fields. Metadata scrolls independently from the read-only, syntax-highlighted body, which fills the available height. Application properties remain beneath the body in a scrollable area capped at 180px. Dead-letter reason, source, and error description stay visible above both panes, with long diagnostics scrolling within 144px.

The persistent header shows subject (or Message details), sequence number, Export, Resend, Clone, and Close. Copy body stays beside the payload. Preserve existing commands, optional-field visibility, selectable metadata, and both themes. Modal margins are 32px; the footer identifies read-only inspection. Routing appears when any supported routing field is present.

### Send Message composer

Send Message retains the native Compose, Properties, and Custom tabs within a modal capped at (960 × 760px), with minimum width (720px) and an outer margin (24px). The destination and Close remain in the fixed header; compact tabs and template tools sit below it. A persistent Send/Cancel footer keeps the body-required helper visible. Send is disabled and reads Sending... while the existing sending state is active.

Compose gives the available height to a native multiline body editor using the existing payload type role and dynamic code foreground, including its placeholder. The editor scrolls natively; template values and long errors have their own bounded scroll areas. Properties groups all existing fields into flat Identity, Routing & sessions, and Delivery sections with two columns. Custom properties uses native key/value rows with Add, Remove, and an explicit empty helper.

The native Templates flyout retains search, Use, Duplicate, and Delete, with compact metadata and an empty label tied to the actual displayed item count. Import, Save template, Update, and Clear retain their existing bindings. No send shortcut is advertised without an implemented binding. Surface dimensions, states, evidence, and limits are recorded in [desktop-send-message.md](../.impeccable/surfaces/desktop-send-message.md); these local choices do not add global desktop tokens.

### Settings dialog

Settings uses a native left-hand TabControl for General, Messages, Security & privacy, Updates, and Help & support. The category rail remains visible while each category scrolls independently. Flat label-and-control rows and group headings provide hierarchy within the existing Fluent dialog; preserve Inter, Lucide icons, dynamic light/dark resources, and violet selection feedback.

Keep Close in the header and Reset to Defaults, Cancel, and Save Changes in the persistent footer. Preserve setting bindings, commands, conditional states, and accessible names when regrouping controls. Surface-specific dimensions, type sizes, and verification scope are recorded in [desktop-settings.md](../.impeccable/surfaces/desktop-settings.md); they do not introduce a desktop-wide token scale.

### Connections manager

Connections uses a focused native list within the existing large modal. Keep the environment filter and Add Connection action fixed above independently scrolling, flat rows. Each row aligns name, endpoint and optional entity with Type, Environment, favorite, edit, delete, and an explicit Connect action. Trim long identity values with full-value tooltips; icon actions retain accessible names and tooltips.

Backup & Management is a native Expander, collapsed initially, below the list. Its passphrase, encrypted export/import, error feedback, and clear-all action remain available without competing with routine connection selection. Add/edit forms scroll between the persistent header and Save/Update Connection and Cancel footer. Connection-check results and detected details wrap within the available form width; validation messages also wrap. Preserve the distinct empty-library and empty-environment states, existing command bindings, storage, and secret handling.

The modal inherits the shared large-dialog bounds with a local (24px) outer margin. Surface-specific dimensions and verification scope are recorded in [desktop-connections.md](../.impeccable/surfaces/desktop-connections.md); they do not introduce new desktop tokens.

### Activity Log

Activity Log remains a right-hand drawer (600px), capped to the available width, with the existing overlay and native close behavior. The fixed header offers Copy visible and Close; a full-width search field sits above separate Level and Source controls. Counts and Newest first remain above the independently scrolling, virtualized entries. Flat rows use thin seams, wrapping messages (14px), selectable wrapping details (13px), and compact timestamp, level, and source metadata (12px). Full timestamps remain available through tooltips.

Keep Auto-scroll, Debug logs, and Clear all in the persistent footer. The two toggles reuse the shared pill style, including its theme-aware checked state. Warning labels use the existing stronger warning foreground. Auto-scroll returns to the newest entry when enabled and follows changes while enabled; disabling it permits reviewing older entries. Preserve copy-entry actions, search, level/source filtering, debug inclusion, and the distinction between an empty library and filters with no matches. Copy visible applies to the filtered entries; Clear all includes hidden entries.

### Entity details

Queue, topic, and subscription details retain a compact read-only native modal with maximum dimensions (720 × 680px) and an outer margin (24px). The title, Property/Value headings, and footer remain fixed around the scrolling property rows. Rows align a property column (180px) and a wrapping value column with a gap (24px); labels use (13px) text and selectable values (14px). Queue and topic sizes explicitly identify bytes.

Preserve each entity's existing property rows and Close commands. Keep the read-only explanation and Close action in the footer, with no editing controls. Surface-specific behavior and evidence for both components are recorded in [desktop-activity-entity-details.md](../.impeccable/surfaces/desktop-activity-entity-details.md); these local dimensions do not extend the global desktop token scale.

## Do's and Don'ts

- **Do** preserve both themes and all message/entity, session, locked-settlement, and bulk operations.
- **Do** use existing Fluent controls, Inter, Lucide icons, and dynamic theme resources.
- **Do** keep an explicit full-details action available on every message row.
- **Do** verify stacked inspection using available pane width and preserve saved navigation preferences.
- **Do** retain entity-pane resized widths when hiding and showing the pane.
- **Don't** apply the website's teal/lime palette or Bricolage Grotesque to desktop controls.
- **Don't** turn ordinary row selection into an automatic modal.
- **Don't** trade inspection space for repeated connection summaries or nested row cards.
- **Don't** claim changed backend behavior or real Azure validation from visual verification.

Implementation evidence: [application theme dictionaries](../BusLane/App.axaml), [shared native styles](../BusLane/Styles/AppStyles.axaml), [window layout](../BusLane/Views/MainWindow.axaml) and [keyboard/pane behavior](../BusLane/Views/MainWindow.axaml.cs), [message workspace](../BusLane/Views/Controls/MessagesPanelView.axaml) and [adaptive layout](../BusLane/Views/Controls/MessagesPanelView.axaml.cs), [message rows](../BusLane/Views/Controls/MessageRowView.axaml), [inspector](../BusLane/Views/Controls/MessageInspectorView.axaml) and [compact behavior](../BusLane/Views/Controls/MessageInspectorView.axaml.cs), [namespace tabs](../BusLane/Views/Controls/TabBarView.axaml), and [theme-aware syntax](../BusLane/Views/Controls/SyntaxHighlightedTextBox.axaml.cs).

Verification scope: native captures use synthetic messages in a temporary harness, in light and dark themes at widths (1440px) and (1000px). These establish layout and rendering evidence, not live Azure behavior.
