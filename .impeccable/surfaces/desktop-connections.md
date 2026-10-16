---
version: 1
slug: "desktop-connections"
primary_target: "BusLane/Views/Dialogs/ConnectionLibraryDialog.axaml"
related_targets: ["BusLane.Tests/Views/ConnectionLibraryDialogTests.cs"]
---

# Desktop connections manager

## Intent and authority

Mode: Operate. The user confirmed a focused saved-connections list with collapsible backup tools, implemented on the current branch. Target: `BusLane/Views/Dialogs/ConnectionLibraryDialog.axaml`.

This surface extends the incumbent [desktop design system](../../docs/desktop-design.md): native Avalonia/Fluent controls, Inter, Lucide icons, violet feedback, and dynamic light/dark resources. Root website design records do not govern this dialog. This is a bounded surface redesign; it introduces no new visual world or global token system.

## Implemented composition

- The shared large modal has maximum dimensions (900 × 720px) and a local outer margin (24px). At a (1000 × 600px) window, the modal retains that margin and its content scrolls within the available height.
- The persistent header shows My Connections, its supporting description, Add Connection in list mode, and Close. A fixed filter strip offers All, Development, Test, and Production plus the visible count.
- Flat saved-connection rows align the identity column with Type (80px), Environment (90px), and actions (220px), separated by column gaps (12px). Name, endpoint, and optional entity trim with full-value tooltips. Row padding is (12px horizontal, 14px vertical); a thin divider and tonal hover provide separation.
- Favorite, edit, and delete remain visible as native icon buttons (34 × 34px), with accessible names and tooltips. Connect is an explicit text action. Environment badges and favorite state retain existing semantics.
- Backup & Management uses a native Expander, initially collapsed. Its independently scrolling content is capped at (220px) and includes a labeled masked passphrase, encrypted Export and Import, backup error feedback, and Clear All Connections. Export and clear-all retain their saved-connection visibility conditions.
- Add/edit forms scroll independently between the header and persistent Cancel and Save Connection/Update Connection footer. Labeled name, environment, and masked connection-string inputs retain Test and its busy state. Success results, detected details, errors, and validation messages wrap within the constrained content width.
- The title is (20px, semibold), form/empty-state headings (18px, semibold), row names (14px, semibold), and supporting text follows the incumbent caption styles. These are surface roles, not a replacement desktop type ramp.

## States and preservation

The empty library says No connections saved and points to adding a connection or importing an encrypted backup. A filter with no matches says No connections in this environment and suggests another environment or adding a connection. Both retain Add Connection.

Preserve existing add/edit/cancel/save, connect, favorite, delete, check-connection, backup, and clear-all bindings and their visible/enabled conditions. The redesign changes layout and presentation only; view-model behavior, connection storage, encryption, and secret handling remain unchanged. Native controls retain their keyboard behavior; icon actions and form inputs expose labels or accessible names.

## Verification and limits

Source evidence: `BusLane/Views/Dialogs/ConnectionLibraryDialog.axaml`, shared modal/control styles in `BusLane/Styles/AppStyles.axaml`, and the desktop theme resources recorded in `BusLane/App.axaml`.

The [native capture matrix](../review/connections/) contains 24 synthetic Avalonia captures: list, add, edit with a long error, edit with a long success result and detected details, filtered-empty, and expanded backup at window sizes (1280 × 800px) and (1000 × 600px), in both themes. The long-diagnostic revision constrains the status icon/text layout with `Auto,*` columns and wraps result/details text. The filtered-empty state is captured with existing saved connections and no visible matches; the empty-library state is evidenced by source visibility bindings rather than a separate capture.

The initial independent review of 20 views found no other material issues beyond a P2 long-diagnostic wrapping finding. The final review marked that finding resolved with a ship disposition at the reviewed scope; the final matrix includes eight long error/success captures across both sizes and themes. The application build is clean and nine focused tests pass. The full suite reports 905 passed, one skipped, and four known website failures. These build/test and review results were supplied by the implementation workflow, rather than inferred from the images.

The captures establish native rendering with synthetic data. They do not establish real Azure connectivity, export/import results, secret persistence, assistive-technology output, or Windows/Linux rendering. This surface uses Avalonia rather than HTML/CSS, so web layout detectors do not apply.
