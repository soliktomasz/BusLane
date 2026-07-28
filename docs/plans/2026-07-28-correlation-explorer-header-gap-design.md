# Correlation Explorer Header Gap Design

## Context

The correlation explorer header currently has a 16-pixel bottom margin. Because the
panel background is darker than the header and command bar, that margin renders as
an unnecessary full-width black strip. The search box also overrides the shared
input background with a transparent fill, making its inner field appear heavier
than the rest of the app.

## Considered approaches

1. Remove the header margin and let the existing one-pixel header border separate
   the sections. Allow the search box to inherit the app input surface. This is the
   selected approach because it matches adjacent app panels and removes special-case
   styling.
2. Keep the margin but recolor it to the command-bar surface. This hides the strip
   but preserves unexplained empty space.
3. Reduce the margin to a smaller spacer. This weakens but does not remove the
   visual disconnection.

## Approved design

Remove the correlation explorer header's bottom margin. Keep its existing bottom
border as the only divider before the command bar. Remove the search text box's
transparent background override so the global `TextBox` style supplies
`InputBackground`. No layout, commands, filtering behavior, or other panels change.

## Verification

Add XAML contract coverage for the absence of the correlation header margin and
transparent search override. Run the focused view tests, then the full test suite
and build.
