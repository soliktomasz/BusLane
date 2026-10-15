# Desktop settings

## Intent and authority

The user chose faster navigation with clear categories. This surface extends the incumbent [desktop design system](../../docs/desktop-design.md): native Avalonia/Fluent controls, Inter, Lucide icons, violet feedback, and dynamic light/dark themes. Website design documents and `.impeccable/design.json` do not govern this dialog.

## Implemented composition

- Five native left-hand tabs: General, Messages, Security & privacy, Updates, and Help & support. General is selected initially; category headers retain icons, labels, and accessible names.
- The modal stretches within a (24px) margin, with maximum size (900 × 680px) and minimum width (740px). The category rail is (196px) wide; category content uses padding (28px horizontal, 24px top, 28px bottom).
- Each category has its own vertical ScrollViewer. The header and footer stay outside the scrolling content. Close invokes Cancel; the footer retains Reset to Defaults, Cancel, and Save Changes.
- Flat rows have a minimum height of (48px), wrapping explanatory text, and trailing native controls. Small buttons have a minimum height of (32px). Group headings and dividers replace repeated nested cards; conditional information panels remain available where needed.
- The dialog title is (20px, semibold), category titles (22px, semibold), group headings (14px, semibold), setting labels (13px, medium), and supporting copy (12px). These are local settings roles, not a replacement global type ramp.

## Preservation and evidence

Source: `BusLane/Views/Dialogs/SettingsDialog.axaml`, with existing resources in `BusLane/App.axaml` and `BusLane/Styles/AppStyles.axaml`. Preserve existing setting bindings, commands, enabled/visible conditions, and native keyboard behavior when editing this surface.

Native rendering was reviewed for all five categories at window sizes (1280 × 800px) and (900 × 650px), in both themes. Captures are in [review/settings](../review/settings/). The independent review disposition was ship with no material fixes. This evidence covers the captured native rendering and source/binding review; Windows/Linux runtime behavior and uncaptured conditional states remain outside that verification scope.
