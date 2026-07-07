# BusLane Warm Command Workspace Design

## Context

BusLane already has a solid desktop application foundation: a left navigation shell, workspace tabs, Azure and connection-string modes, entity browsing, message inspection, dashboards, live stream, logs, terminal access, and a broad set of dialogs. The current design is functional and Fluent-inspired, but it lacks a single cohesive product identity. Many surfaces use similar neutral cards, soft borders, and repeated icon tiles, which makes the app feel polished in parts but not bold or unified as a whole.

This design proposes a visual and shell refresh, not a workflow rebuild. The goal is to make BusLane feel like one deliberate product while preserving the existing MVVM structure and core Service Bus workflows.

## Direction

BusLane should become a **Warm Command Workspace**: a polished Service Bus desktop tool that feels more branded, more deliberate, and more confident without becoming decorative or slow to scan.

The redesign should be warm, not soft. It should use warmer neutral surfaces with the existing indigo/violet logo accent as the brand color, while preserving strong contrast, compact density, and clear operational state.

The app shell should become one clear system. The sidebar, tab bar, entity pane, message pane, dashboard, dialogs, status bar, terminal, and log surfaces should share the same tokens, spacing, state language, and component anatomy.

Operational hierarchy remains the first priority. Current workspace, selected entity, message status, dead letters, live stream state, and destructive actions must be visually unmistakable.

Boldness should come from structure: stronger layout rhythm, better active states, richer headers, and tighter component language. Avoid loud gradients, marketing-style hero sections, oversized decoration, and card-heavy layouts that reduce scan speed.

## Scope

The redesign plan should include shell and hierarchy refresh work:

- Shared theme resources and component styles.
- Navigation sidebar and collapsed rail.
- Workspace tab bar.
- Main window shell, status bar, terminal entry point, and log entry point.
- Entity pane and message pane.
- Dashboard, live stream, alerts, logs, and terminal panels.
- Major dialogs and modal overlays.

The plan should not replace the navigation model, rewrite workflows, or redesign the application as a dashboard-first product.

## Visual System

### Palette

Replace the neutral Fluent blue brand emphasis with a warmer BusLane palette that keeps the existing logo accent.

- Brand accent: indigo/violet from the existing BusLane logo for primary identity, selected emphasis, and key action highlights.
- Shell anchor: deep graphite/slate for strong text, chrome, and structural contrast.
- Light surfaces: warm off-white, stone, and muted sand-tinted neutrals.
- Dark surfaces: warm charcoal, graphite, and brown-neutral dark layers.
- Status colors: preserve distinct success, warning, danger, and info colors. Orange should remain available for warning and must not become the brand accent.

The brand accent should be used deliberately. It should identify BusLane and selected workflow context, not color every interactive element.

### Typography

Keep the current practical type ramp, but make hierarchy more intentional.

- Pane and page headers should be larger and weightier than ordinary section headings.
- Metadata and captions should be smaller, quieter, and consistent across panes.
- Monospace should be reserved for IDs, entity paths, sequence numbers, JSON, payloads, and other technical values.
- Dense controls should avoid oversized text.

### Surfaces

Reduce the feeling that every section is a card.

- Use full-height pane surfaces for the entity and message workspace.
- Use section bands or unframed layouts for page-level structure.
- Keep cards for repeated items, modal bodies, metric widgets, and genuinely grouped tools.
- Avoid card-inside-card patterns.
- Use borders, background steps, and header zones to distinguish structure before adding shadows.

### Buttons

Use button emphasis more selectively.

- Primary buttons are for main workflow actions such as Send, Connect, Save, Unlock, and Create.
- Secondary buttons are for ordinary commands.
- Icon buttons are for repeated utility actions where the icon is familiar and has a tooltip.
- Danger actions remain red or danger-styled. They must not use the brand orange.
- Toolbar actions should be compact and consistent across panes.

### Badges And States

Standardize state badges and visual indicators.

- Environment badges.
- Connected/live badges.
- Dead-letter badges.
- Active, scheduled, and session badges.
- Selected entity indicators.
- Loading and refresh states.
- Unread error indicators.

Dead-letter and destructive states should have enough contrast to remain obvious in both light and dark themes.

### Icons

Continue using Lucide icons. Standardize repeated meanings:

- `Inbox` or `ListOrdered` for queues and messages.
- `Waypoints` for namespace or workspace context.
- `TriangleAlert` for dead letters and problems.
- `Radio` for live stream.
- `Library` for saved connections.
- `Terminal` and `ScrollText` for the utility dock.

## Shell And Layout

### Sidebar

The sidebar becomes the brand and workspace anchor.

The expanded sidebar should include a stronger BusLane identity block, a more distinctive active workspace summary, and clearer separation between workspace actions, quick switch, and utility navigation. It should feel like an application command surface instead of a vertical stack of unrelated cards.

The collapsed rail should share the same brand language. It should use the same logo treatment, compact utility icons, and clear active or attention states.

### Tab Bar

The tab bar remains functional but becomes quieter.

Workspace tabs should visually recede behind the active workspace. Active and inactive states need stronger clarity, but the bar should not feel like browser chrome. The New Tab action can become a compact icon-plus command with a tooltip.

### Main Workspace

The main workspace should read as two deliberate panes: entity navigation and message operations.

Both panes should use shared pane anatomy:

- Header row with icon, title, context subtitle, and primary actions.
- Optional search or filter row.
- Content region.
- Optional footer or status row.

The selected queue, topic, or subscription should be visually obvious. Active versus dead-letter message views, session scope, and search/filter state should also be clear without requiring the user to read every small label.

### Dashboard

The dashboard remains an operational overview rather than the app's default center of gravity.

It should answer "what needs attention?" first:

- Dead letters.
- High-volume entities.
- Scheduled messages.
- Namespace size.
- Stale, empty, or loading states.
- Recent or live activity where available.

Metric widgets should feel connected to the rest of the app through shared warm tokens and state colors.

### Utility Dock

The status bar, terminal panel, and log viewer should be visually aligned with the shell.

They should feel compact, readable, and secondary. Error and unread states should stand out enough to be useful without overpowering the main workspace.

### Dialogs

Major dialogs should use a shared command pattern:

- Icon.
- Title.
- Short context line.
- Primary action area.
- Secondary and destructive actions separated clearly.
- Consistent modal border, radius, padding, and surface treatment.

Priority dialogs for the redesign pass are Send Message, Connection Library, Settings, Entity Detail/Edit, Subscription flows, Command Palette, Device Code, App Lock, and confirmation dialogs.

## Implementation Phases

### Phase 1: Design Tokens

Update shared theme resources and global styles first:

- Light and dark palettes.
- Surface layers.
- Border and focus tokens.
- Typography selectors.
- Buttons.
- Badges.
- Pane headers.
- Utility bars.
- Common modal styles.

This phase should make the application more cohesive before individual screens are heavily edited.

### Phase 2: Shell Refresh

Apply the new system to:

- `NavigationSidebar`.
- `TabBarView`.
- `MainWindow`.
- Status bar.
- Terminal and log entry points.
- App-level overlays.

This is the phase where the app should start feeling like one product.

### Phase 3: Core Workspace Refresh

Redesign the entity and message panes around the shared pane anatomy.

Focus on:

- Stronger pane headers.
- Clear selected entity state.
- Clear active/dead-letter/session state.
- Consistent search and toolbar treatment.
- Reduced nested-card feel.
- Better action hierarchy.

### Phase 4: Dashboard And Utility Panels

Bring dashboard, live stream, alerts, logs, and terminal into the same visual system.

The dashboard should prioritize attention and operational state over decorative metrics.

### Phase 5: Dialog Pass

Standardize the major dialogs using the shared dialog command pattern.

The goal is not to redesign every dialog workflow, but to make the dialogs feel like siblings within the same product.

### Phase 6: Final Polish And QA

Perform focused visual QA across:

- Light and dark themes.
- Minimum window size.
- Long workspace names and entity paths.
- High message counts.
- Empty states.
- Loading states.
- Focus rings.
- Keyboard workflows.
- Accessibility contrast.
- Destructive action clarity.

## Risks

Warm neutrals with a violet accent can feel less obviously "warm" than an orange-led palette if surfaces stay too cool. Keep the shell graphite/stone/sand-tinted, and reserve orange for warning states.

More brand personality can reduce scan speed if spacing grows too much. The app should remain compact and utilitarian.

Shell changes may expose older one-off component styles. Starting with tokens and shared styles reduces that risk.

Dialog consistency may be larger than expected because many dialogs currently use custom layouts. The dialog pass should prioritize high-traffic and high-risk dialogs first.

## Verification

Run:

```bash
rtk dotnet build
```

Perform manual smoke checks for:

- Connect and disconnect.
- Namespace selection.
- Entity selection.
- Message loading.
- Dead-letter view.
- Send Message dialog.
- Dashboard.
- Live Stream.
- Settings.
- Command Palette.
- Terminal and log toggles.
- App Lock overlay if enabled.

Visual QA should confirm:

- Light and dark themes are both coherent.
- Button text does not overflow at minimum app size.
- Long entity names and connection names trim cleanly.
- Danger actions remain visually distinct from brand actions.
- Focus rings are visible.
- Dense message/entity lists remain scannable.
