# GitHub Page Control Room Redesign

## Goal
Redesign the GitHub Pages site (`docs/index.html`) so it no longer resembles a generic AI product landing page while preserving current content and section structure.

## Constraints
- Preserve all existing sections and key copy (Features, Roadmap, Platforms, Installation, Security, CTA)
- Keep existing links and functionality (install tabs, copy button, smooth scrolling)
- Ensure responsive behavior for desktop and mobile
- Maintain strong contrast and keyboard-visible focus states

## Chosen Direction: Control Room
A technical, operations-console visual language:
- Graphite/steel surfaces with cyan signal accents and amber warning accents
- Typographic system: `Space Grotesk` + `IBM Plex Mono`
- Panel-based layout with seam lines, telemetry chips, and restrained glow
- Motion focused on reveal and status interaction rather than glossy gradient effects

## Component-Level Design
- **Navigation:** Sticky bar with blueprint stripe and framed CTA
- **Hero:** Operations-console framing with status pill, telemetry strip, and screenshot in a monitor bezel
- **Feature/Security Cards:** Metallic panels with edge markers and subtle topographic overlays
- **Roadmap:** Alternating timeline retained, visually restyled as mission phases
- **Installation:** Console switch tabs and terminal-like code surfaces
- **CTA/Footer:** Heavier industrial framing to close the page with clearer hierarchy

## Accessibility & Responsiveness
- High-contrast text/background combinations
- Visible focus rings for links/buttons/tabs
- Breakpoints for nav wrapping, single-column cards, and stacked timeline on mobile

## Verification
- Open page locally and verify styles load correctly
- Check section anchor scrolling
- Validate install tab switching and copy button behavior
- Confirm no horizontal overflow on mobile widths
