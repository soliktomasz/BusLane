# GitHub Page Control Room Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the current generic landing-page styling in `docs/index.html` with the approved control-room visual system while preserving content and behavior.

**Architecture:** Keep a single static HTML file with embedded CSS/JS. Rework design tokens and component styling classes in place, then adjust markup only where needed for new visual wrappers while retaining IDs, links, and interaction hooks.

**Tech Stack:** HTML5, CSS3, vanilla JavaScript, Lucide icons, Google Fonts

---

### Task 1: Replace design tokens and global styling

**Files:**
- Modify: `docs/index.html`

1. Define new color, spacing, border, and glow variables for control-room look.
2. Replace font stack with `Space Grotesk` and `IBM Plex Mono`.
3. Add global background layers (grid/noise/radial accents) and accessible focus styles.

### Task 2: Restyle structural sections without changing content scope

**Files:**
- Modify: `docs/index.html`

1. Restyle nav, hero, cards, roadmap, platforms, installation, security, CTA, and footer as panel-based components.
2. Preserve section IDs and links.
3. Keep all existing content blocks and action links.

### Task 3: Preserve and verify interactive behavior

**Files:**
- Modify: `docs/index.html`

1. Keep install tab switching logic.
2. Keep copy button behavior.
3. Keep smooth-scroll anchor behavior.
4. Ensure icon initialization still runs after dynamic updates.

### Task 4: Validate and polish responsive behavior

**Files:**
- Modify: `docs/index.html`

1. Add breakpoints for timeline collapse, card stacking, and nav wrapping.
2. Verify no horizontal overflow.
3. Verify tappable controls remain readable and usable.

### Task 5: Verification

**Files:**
- Modify: `docs/index.html`

1. Run a quick static sanity check of the final file.
2. Confirm CSS/JS blocks are syntactically complete.
3. Optionally run `dotnet build` to ensure no unrelated project breakage from doc changes.
