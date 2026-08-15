# Scheduled Message Filter Selection Design

## Problem

`ScheduledMessagesViewModel.NotifyProjectionChanged()` raises property-change notifications for both filtered results and the computed connection/entity option lists. Changing either selected filter calls this method. Avalonia consequently receives a new `ItemsSource` instance for both filter ComboBoxes while it is committing selection, re-synchronizes selection, writes the selected values back to the view model, and repeats until the process overflows the stack.

## Design

Separate the two notification responsibilities:

- Filter changes notify only `FilteredEntries`, `CalendarDays`, and `IsEmpty`.
- Refreshes that replace `Entries` additionally notify `ConnectionOptions` and `EntityOptions`.
- After refresh, retain `SelectedConnection` and `SelectedEntity` when their values remain in the refreshed option lists. Reset a missing value to `"All"`.

This fixes the feedback loop at its source while preserving the existing computed-list API and XAML bindings.

## Testing

Add focused view-model regression tests that verify:

- Changing connection or entity selection does not raise option-list property notifications.
- Refresh retains connection/entity selections that still exist.
- Refresh resets connection/entity selections that no longer exist.

Run the focused view-model tests, then the full test suite and build.
