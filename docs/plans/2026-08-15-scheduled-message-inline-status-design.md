# Scheduled Message Inline Status Design

## Problem

Scheduled-message list rows render the message ID above the local and broker status indicators because the message/status container uses the default vertical `StackPanel` orientation.

## Approved Design

Render the existing message ID and status group in one horizontal row. Add horizontal orientation and spacing to the current message/status container while preserving the nested status group, all bindings, visibility conditions, styles, grid columns, and compact action flyout.

This is the smallest layout-only change. A two-column grid would add unnecessary structure, while combining the values into one binding would remove the existing independent styling and visibility behavior.

## Testing

Extend the scheduled-messages structural view tests first. Locate the `TextBlock` bound to `Entry.MessageId`, assert that its immediate parent is a horizontal `StackPanel`, verify the test fails against the vertical layout, then add the minimal XAML attributes and run focused tests, the full suite, and a full build.
