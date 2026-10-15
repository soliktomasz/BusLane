---
name: "BusLane GitHub Pages website"
description: "Engineering product catalog with real workspace screenshots, cool surfaces, deep teal, and lime controls."
colors:
  ground: "#eef3f4"
  paper: "#ffffff"
  ink: "#123f49"
  muted: "#4c666b"
  line: "#c9d6d8"
  lime: "#cbe983"
  ink-hover: "#205865"
  control-hover: "#dee8e6"
  lime-selected-hover: "#bbdc6e"
  lime-hover: "#d9f49f"
  monitor-surface: "#dce8e7"
  inverse-muted: "#c7d9dc"
  inverse-line: "#54737a"
  focus: "#596e21"
  term-line: "#9bb3b6"
typography:
  display:
    fontFamily: "Bricolage Grotesque, sans-serif"
    fontSize: "clamp(3rem, 6.1vw, 5.25rem)"
    fontWeight: 600
    lineHeight: 1.03
    letterSpacing: "-.035em"
  headline:
    fontFamily: "Bricolage Grotesque, sans-serif"
    fontSize: "clamp(2.15rem, 3.5vw, 3.25rem)"
    fontWeight: 600
    lineHeight: 1.12
    letterSpacing: "-.025em"
  title:
    fontFamily: "Bricolage Grotesque, sans-serif"
    fontSize: "1.6rem"
    fontWeight: 600
    lineHeight: 1.22
    letterSpacing: "-.015em"
  body:
    fontFamily: "Arial, Helvetica, sans-serif"
    fontSize: "17px"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "Arial, Helvetica, sans-serif"
    fontSize: "15px"
    fontWeight: 600
  code:
    fontFamily: "ui-monospace, SFMono-Regular, Consolas, monospace"
    fontSize: "14px"
    lineHeight: 1.85
rounded:
  panel: "12px"
  button: "8px"
  control: "6px"
  copy: "5px"
spacing:
  compact: "12px"
  body: "24px"
  column: "70px"
  section: "100px"
  section-tablet: "76px"
  section-mobile: "58px"
components:
  button-primary:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.paper}"
    rounded: "{rounded.button}"
    padding: "14px 24px"
  button-primary-hover:
    backgroundColor: "{colors.ink-hover}"
  button-small:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.paper}"
    rounded: "{rounded.button}"
    padding: "9px 19px"
  button-lime:
    backgroundColor: "{colors.lime}"
    textColor: "{colors.ink}"
    rounded: "{rounded.button}"
    padding: "14px 24px"
  button-lime-hover:
    backgroundColor: "{colors.lime-hover}"
  workspace-control:
    textColor: "{colors.muted}"
    padding: "14px 11px"
  workspace-control-selected:
    backgroundColor: "{colors.lime}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
  navigation:
    textColor: "{colors.ink}"
    typography: "{typography.label}"
  capability-term:
    textColor: "{colors.ink}"
  code-panel:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
  faq:
    textColor: "{colors.ink}"
---

# Design System: BusLane GitHub Pages website

## Overview

**Creative North Star: "Engineering product catalog"**

This system covers only the static GitHub Pages website in docs/. It does not define the Avalonia desktop application or recolor its screenshots. The approved engineering product catalog uses cool paper, deep teal, lime selection, expressive display type, and plain body text to make technical information clear.

Real application images provide visual evidence. Thin rules organize content; generous spacing separates sections while compact controls keep navigation direct. Rounded screenshot plates carry depth, with restrained movement tied to state changes.

**Key Characteristics:**
- Cool paper and deep teal, with lime for selection and inverse actions.
- Bricolage Grotesque headings paired with Arial body text.
- Rule-separated content, generous screenshot plates, compact controls.
- Keyboard-visible interactions and a stacked mobile layout.

## Colors

Cool neutrals carry reading content; deep teal establishes hierarchy; lime identifies selected controls and the inverse download action. Frontmatter contains normative values, named after their source roles.

### Primary

- **Deep Teal Ink:** headings, body foreground, dark buttons, and inverse download surface. Ink Hover provides button feedback.
- **Workspace Lime:** selected screenshot controls, text selection, and the download action on deep teal. Selected Hover and Lime Hover preserve distinct interaction contexts.

### Neutral

- **Cool Ground / White Paper:** page canvas and code or image surfaces.
- **Muted Teal:** supporting paragraphs, labels, captions, and notes.
- **Quiet Line / Term Line:** section dividers and underlined capability terms.
- **Monitor Surface:** slightly deeper cool ground for the monitoring band.
- **Inverse Muted / Inverse Line:** supporting text and dividers on deep teal.
- **Control Hover:** pale neutral feedback on unselected screenshot controls.
- **Olive Focus:** visible keyboard outline on light surfaces; inverse download region uses lime instead.

## Typography

Display font is locally hosted Bricolage Grotesque with sans-serif fallback. Body and controls use Arial, Helvetica, sans-serif; commands use the system monospace stack. Frontmatter records the desktop hierarchy. Headings balance line breaks and carry weight (600); branding uses weight (700).

- **Display:** primary heading; on mobile, size becomes `clamp(2.65rem, 9.5vw, 4.1rem)` with line-height (1.05).
- **Headline:** section headings; download heading uses `clamp(2.5rem, 4.5vw, 4rem)`.
- **Title:** capability headings; mobile uses (25px). Platform and security headings have smaller contextual sizes.
- **Body:** reading text, with mobile size (16px) and paragraph measure capped at (72ch). Introductory hero copy uses (20px), line-height (1.55), then (17px) on mobile.
- **Label:** navigation and text actions. Small notes and captions range from (12px) to (14px); they remain supporting content.
- **Code:** source commands, reduced to (12px) on mobile; inline code uses (.88em).

**The Legible Hierarchy Rule.** Reserve Bricolage Grotesque for headings and branding; use plain sans-serif for body and controls.

## Layout

The centered shell caps at (1280px), with total horizontal subtraction (88px). At widths at or below (1000px), subtraction becomes (48px); at or below (700px), it becomes (36px). Section padding follows the frontmatter desktop, tablet, and mobile steps.

The workspace combines a fixed (200px) index with a flexible screenshot stage and gap (28px). Tablet index shrinks to (165px), gap (22px). On mobile it stacks; screenshot controls form two equal columns, with gaps (5px) and minimum height (44px). Secondary workspace notes hide on mobile while screenshot captions remain.

Section introductions, capability rows, and connection paths use two columns with the shared column gap. Monitoring, source, and FAQ use asymmetric columns. At the mobile breakpoint these become one column with gap (24px). Header navigation wraps into a second row; footer stacks. Release platforms retain three columns. Code panels permit horizontal scrolling and have `min-width: 0` to contain overflow. Wide screens at or above (1600px) increase hero top padding to (52px).

## Elevation & Depth

Most content is flat: cool tonal bands and one-pixel rules establish grouping. Screenshots use soft ambient shadows, with a smaller shadow on mobile. White code panels use a border rather than elevation. Buttons gain a modest shadow on hover. Exact shadow and motion values live in the sidecar.

**The Evidence First Rule.** Use real product screenshots at their natural aspect ratio, with access to full-size images.

State changes use short ease-out transitions. Screenshot selection crossfades opacity and saturation over (.3s), without moving the image. Reduced-motion preferences remove transitions and animation and disable smooth scrolling.

## Shapes

Screenshot and code-panel corners use the panel radius; buttons use the button radius. Selected screenshot controls use the control radius; the copy button uses the copy radius. Mobile screenshots use the button radius. Rules and bordered panels provide structure; capability terms are underlined text, not filled pills. Icons are inline stroked SVG, normally (20px), with rounded line ends.

## Components

### Buttons

Primary anchors are deep teal with white text, padding from frontmatter, minimum height (54px), weight (600), and size (16px). Small header variant has minimum height (42px) and size (14px). Lime inverse variant appears on the download band. Hover changes fill and adds the shared shadow; keyboard focus uses an outline (3px) with offset (5px). Inverse-region outlines use lime.

### Navigation

Brand combines the existing app icon with Bricolage Grotesque. Plain text navigation uses weight (600), no underline at rest, and underline on hover. Desktop link gap is (30px); tablet uses (20px). Mobile links spread across a full-width second row at size (12px). Skip link becomes visible on focus.

### Workspace selector and screenshot plate

Button rows have divider rules, muted text, and minimum height (48px). Selected `aria-pressed` state uses lime, ink, weight (600), and a visible arrow. Unselected hover uses Control Hover; selected hover uses Selected Hover. Arrow keys cycle controls; Home and End select endpoints. JavaScript reveals controls; without it, the default screenshot and additional image links remain available. Figures keep natural image proportions and captions with full-size links.

### Capability terms and rows

Capability articles are two-column, border-top rows with vertical padding (32px), reduced to (26px) on mobile. Terms are wrapping, weight (600) text labels at (12px), underlined with Term Line and bottom padding (3px). They are informational, not interactive chips.

### Source code panel

White, bordered container clips to the panel radius. Header padding is (14px 22px); command padding is (24px 22px). Copy button uses ground fill, a thin border, compact padding (6px 10px), and lime hover. Disabled copying has opacity (.6) and waiting cursor. A reserved status line reports copy success or manual-copy fallback. The button stays hidden when clipboard support is absent.

### FAQ

Native `details` and `summary` elements provide disclosure behavior. One-pixel bottom rules separate items. Summary padding is (20px) vertically with (16px) right padding; answers use muted text with padding (0 0 24px 20px). Native markers remain visible. Keyboard focus follows the shared outline.

## Do's and Don'ts

### Do:

- **Do** preserve real screenshot proportions and full-size links.
- **Do** use thin dividers and spacing to separate technical content.
- **Do** retain pressed, hover, keyboard-focus, and reduced-motion states.
- **Do** keep mobile controls readable and code horizontally scrollable.

### Don't:

- **Don't** apply this website palette to the desktop application.
- **Don't** replace product screenshots with decorative mockups.
- **Don't** turn every text section into an elevated card.
- **Don't** introduce unimplemented form fields, modal dialogs, or input patterns into this record.
