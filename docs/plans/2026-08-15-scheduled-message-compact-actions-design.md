# Scheduled Message Compact Actions Design

## Problem

Scheduled-message list rows place fixed-width metadata columns, a flexible message/status column, and four inline action buttons in one grid row. At smaller window widths, the action group consumes enough horizontal space to overlap the message/status content.

## Approved Design

Replace the four inline action buttons with one compact, accessible action trigger in the existing Actions column. The trigger uses BusLane's established `Button.Flyout` pattern and opens a right-aligned flyout containing Clone, Cancel, Reschedule, and Resolve as full-text actions.

Each flyout action continues to use the existing view-model command and the scheduled-message row as its command parameter. The trigger receives an automation name and remains keyboard accessible. No view-model state, command, theme token, or service change is required.

This design keeps all operations discoverable while reducing the Actions column from four buttons to one compact control, leaving the message/status column enough room at smaller widths.

## Alternatives Considered

- Four icon-only buttons reduce width but remain less compact and less discoverable.
- Horizontal scrolling prevents visual overlap but makes row actions harder to reach.

## Testing

Extend the scheduled-messages XAML contract test first so it requires the compact action trigger, flyout placement, all four existing commands, and removal of the inline horizontal action stack. Verify the test fails against the current view, implement the minimal XAML change, then run the focused view test, the full test suite, and a full build.
