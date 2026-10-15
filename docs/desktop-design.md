---
name: "BusLane desktop workspace"
description: "Existing Fluent desktop system, refined for browsing entities and inspecting messages."
colors:
  brand-light: "#4F46E5"
  brand-dark: "#A5B4FC"
  canvas-light: "#FAF9F5"
  canvas-dark: "#18181B"
  foreground-light: "#27272A"
  foreground-dark: "#F4F4F5"
  subtle-light: "#52525B"
  subtle-dark: "#A1A1AA"
typography:
  body:
    fontFamily: "Inter, Segoe UI Variable, SF Pro Display, Segoe UI, San Francisco, Helvetica Neue, Arial"
    fontSize: "14px"
    lineHeight: 20
  preview:
    fontFamily: "Cascadia Code, Consolas, Monaco, monospace"
    fontSize: "12px"
rounded:
  small: "6px"
  medium: "8px"
  large: "12px"
components:
  message-row:
    rounded: "0px"
    padding: "10px 12px"
  message-search:
    rounded: "{rounded.small}"
    padding: "6px 8px"
---

# Design System: BusLane desktop workspace

## Overview

This record covers Avalonia desktop UI only. Root [DESIGN.md](../DESIGN.md), [PRODUCT.md](../PRODUCT.md), and `.impeccable/design.json` describe the website; their palette and type choices do not govern desktop controls.

Refine the incumbent warm command workspace. Preserve BusLane violet, Fluent controls, Inter text, Lucide icons, both themes, and existing workflows. Prioritize browsing entities and inspecting messages by reducing repeated connection context and giving message content more space. This is an extension of the existing system, implemented directly in code; no replacement identity or new imagery is introduced.

## Colors

Primary violet identifies actions, selection, and focused splitter feedback. Canvas, foreground, and subtle text switch through `App.axaml` theme dictionaries; frontmatter records representative light/dark pairs, not a second runtime theme source.

**The Live Theme Rule.** Use dynamic theme resources. Entity-selection bindings also observe `Window.ActualThemeVariant`, so changing theme refreshes selected queue, topic, and subscription presentation.

Placeholders use `SubtleForeground` with `TextControlPlaceholderOpacity` (1). Workspace tab subtitles also retain opacity (1); avoid multiplying a deliberately subtle foreground by additional transparency.

## Typography

Inter with existing platform fallbacks remains the UI family. Shared body text uses the frontmatter role; message previews use the existing monospace stack. The Messages title is compact (16px). Shared styles retain the broader Fluent type ramp for other surfaces.

Sequence and date metadata align in fixed columns. Full enqueue timestamps retain the year (`yyyy-MM-dd HH:mm:ss`); do not shorten diagnostic context to achieve density.

## Layout

Sidebar width is (256px), with aligned sidebar and tab headers (56px). Azure and connection-string workspaces share a resizable entity/message split: entity pane starts at (280px), minimum (220px); native splitter is (5px); message pane minimum is (420px). Hiding the entity pane releases its width; showing it restores the previous widths for each workspace grid during the window lifetime.

Message title and session context sit opposite Send. Search uses a native TextBox with leading search icon and inline clear action; Select, Refresh, and More form a compact utility group beside it. Refresh and More are icon-only, with tooltips and accessible names. The search row remains usable at the 420px message-pane minimum. Session context can wrap; session labels are capped at (180px), truncate, and expose their full value through a tooltip. Message rows use fixed sequence (70px) and timestamp (126px) columns, flexible message identity, and trailing actions, with column gaps (8px). Command and entity search surfaces use padding (16px horizontal, 12px vertical); header rows use a (12px) gap. Message search, Send, Select, Refresh, and More use a (36px) minimum height; the inline clear action uses (30px). The search prompt explicitly scopes filtering to loaded messages.

**The Inspection Space Rule.** Keep repeated context compact; preserve readable message metadata, search, and resizable inspection space.

## Elevation & Depth

Message rows are flat, separated by a bottom divider (1px), with no shadow. Hover uses `SurfaceSubtle`; message previews and sequence labels have transparent backgrounds. Sidebar context uses a divider instead of a raised summary card. These are workspace patterns, not a ban on existing dialogs or other elevated surfaces.

## Shapes

Shared desktop radii remain small controls (6px), buttons (8px), and cards/dialogs (12px). Message rows and their previews have square corners; search keeps the small radius. Retain existing rounded badges where they convey state.

## Components

### Entity navigation

Retain Azure Identity and connection-string trees, search, entity commands, and selected-state feedback. The splitter remains a native, keyboard-focusable control with violet hover/focus feedback. Entity-pane restore remains available beside the Messages title.

### Message workspace

Keep primary operations visible and secondary commands available through More. Deferred receive lives inside More under “Receive deferred messages,” retaining sequence input and its command. Command relocation must preserve behavior, visibility conditions, and keyboard access.

Rows use the frontmatter padding and flat divider treatment. Message action buttons remain visible at rest. Preserve both active and dead-letter message presentations, selection, session controls, search/filter states, and detail inspection.

### Settings dialog

Settings uses a native left-hand TabControl for General, Messages, Security & privacy, Updates, and Help & support. The category rail remains visible while each category scrolls independently. Flat label-and-control rows and group headings provide hierarchy within the existing Fluent dialog; preserve Inter, Lucide icons, dynamic light/dark resources, and violet selection feedback.

Keep Close in the header and Reset to Defaults, Cancel, and Save Changes in the persistent footer. Preserve setting bindings, commands, conditional states, and accessible names when regrouping controls. Surface-specific dimensions, type sizes, and verification scope are recorded in [desktop-settings.md](../.impeccable/surfaces/desktop-settings.md); they do not introduce a desktop-wide token scale.

## Do's and Don'ts

- **Do** preserve both themes, all message/entity operations, and existing navigation/tool workflows.
- **Do** use existing Fluent, Inter, Lucide, and theme resources; no new assets are needed.
- **Do** verify entity hide/show restores resized pane widths and theme changes update selections.
- **Don't** apply website teal/lime or Bricolage Grotesque to desktop UI.
- **Don't** replace usable inspection width with repeated connection summaries or nested row cards.
- **Don't** claim backend behavior or capabilities changed through this visual refinement.

Implementation evidence: `BusLane/App.axaml`, `BusLane/Styles/AppStyles.axaml`, `BusLane/Views/MainWindow.axaml` and code-behind, plus `NavigationSidebar`, `TabBarView`, both entity trees, and `MessagesPanelView` under `BusLane/Views/Controls/`. Surface direction lives in [desktop-workspace.md](../.impeccable/surfaces/desktop-workspace.md).

### Message details: split inspector

Message details uses the approved Split inspector layout. A fixed 260px metadata rail shows identity, timing, delivery, and conditional routing fields. Metadata scrolls independently from the read-only, syntax-highlighted body, which fills the available height. Application properties remain beneath the body in a scrollable area capped at 180px. Dead-letter reason, source, and error description stay visible above both panes, with long diagnostics scrolling within 144px.

The persistent header shows subject (or Message details), sequence number, Export, Resend, Clone, and Close. Copy body stays beside the payload. Preserve existing commands, optional-field visibility, selectable metadata, and both themes. Modal margins are 32px; the footer identifies read-only inspection. Routing appears when any supported routing field is present.
