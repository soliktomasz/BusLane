# Correlation Explorer Live Filtering and Comparison Design

## Goal

Turn the Correlation Explorer into a live investigation workspace by adding:

- automatic updates while the explorer is open;
- catalog-wide structured filtering;
- comparison of any two messages, including a quick comparison with the previous timeline entry.

This iteration does not add persistence, cross-namespace replay, batch replay, or OpenTelemetry-based grouping.

## Architecture

### Catalog notifications

`ICorrelationMessageCatalog` will expose a lightweight change event. The event identifies the affected correlation or session group keys and the kind of mutation without including message bodies, application properties, credentials, or connection strings.

`CorrelationMessageCatalog` will mutate its bounded collection under its existing per-instance lock, capture the notification data, release the lock, and only then notify subscribers. This prevents UI work from blocking ingestion while the catalog lock is held.

Range additions may be represented by one coalesced notification rather than one notification per message.

### Explorer lifecycle

`CorrelationExplorerViewModel` will subscribe while active and unsubscribe when disposed. `FeaturePanelsViewModel` will dispose the explorer when the panel closes or is replaced.

Catalog changes will be marshalled to the Avalonia UI thread and coalesced over a short interval. This avoids rebuilding observable collections for every message in a streaming burst.

### Filtering

An immutable filter model will represent:

- free-text search;
- start and end time;
- namespace;
- entity;
- environment;
- loaded or live-stream source;
- correlation or session identifier;
- application-property key and value.

Filters apply across the complete in-memory catalog. A correlation group remains visible when at least one of its messages matches. Its displayed timeline contains only matching messages.

When filters or live data change, the explorer restores the selected group and message by stable identity whenever they remain visible. Otherwise it selects the nearest available result. A clear action resets all filters.

### Message comparison

A stateless comparison service will accept two immutable `CorrelationMessage` values and return:

- changed standard metadata fields;
- added, removed, and modified application properties;
- body comparison data;
- enqueue-time difference;
- source entity and environment changes.

When both bodies are valid JSON, comparison will use normalized JSON structure so insignificant formatting differences are ignored. Invalid or non-JSON payloads fall back to plain text comparison.

The ViewModel will expose two comparison slots. Users may assign any two timeline messages or use a command that compares the selected message with its previous chronological entry.

## Interaction design

The filter area will be collapsible to preserve space in the existing three-column layout. Active filters will be visible and removable without clearing unrelated criteria.

New catalog messages will update matching groups automatically. The explorer will not move the current selection. When the selected group receives additional messages, a non-intrusive new-message count will appear. The user can acknowledge it by navigating to the latest entry.

Timeline rows will expose actions for assigning comparison message A or B. The details pane will add a Compare tab that displays body, metadata, property, timing, and source differences.

If a compared message is evicted by the bounded catalog, only that comparison slot is cleared and a concise status message is shown.

## Error handling

- Catalog event handlers must not run under the catalog lock.
- UI refresh failures must surface through `StatusMessage` without interrupting message ingestion.
- Malformed JSON must fall back to plain text comparison.
- Invalid filter text or time values must produce validation feedback without clearing the current results.
- Disposal must cancel pending refresh work and detach all subscriptions.

## Performance

The existing 2,000-message catalog capacity remains unchanged. Notifications contain identifiers only. Streaming bursts are coalesced before observable collections are rebuilt.

The first implementation will continue to derive filtered groups from a catalog snapshot because the bounded size makes this predictable and simple. The event contract will allow a future incremental or Reactive Extensions implementation without changing ingestion callers.

## Testing

Tests will cover:

- catalog notifications for add, replacement, eviction, range addition, and clear;
- subscriber invocation after the catalog lock is released;
- catalog-wide filter combinations;
- selection preservation across filtering and live updates;
- automatic refresh and burst coalescing;
- no automatic selection changes when messages arrive;
- subscription cleanup when the explorer closes;
- arbitrary two-message and previous-message comparison;
- JSON, text, metadata, property, timing, and source differences;
- missing or evicted comparison messages;
- filter and comparison XAML bindings;
- full build and regression suite.
