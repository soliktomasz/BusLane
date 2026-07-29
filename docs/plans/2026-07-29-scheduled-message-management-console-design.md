# Scheduled-Message Management Console Design

**Date:** 2026-07-29
**Issue:** [#160 — Add a scheduled-message management console](https://github.com/soliktomasz/BusLane/issues/160)

## Goal

Extend BusLane's existing scheduled-send support into a global lifecycle-management console for messages scheduled through BusLane. Users can find locally indexed schedules across saved connections and entities, distinguish local knowledge from broker-confirmed outcomes, and safely clone, cancel, reschedule, refresh, or resolve entries.

Azure Service Bus does not provide an API that enumerates every scheduled message or verifies an individual scheduled sequence number. The console therefore treats the local index as a management aid rather than an authoritative broker inventory.

## Architecture

The console is a global workspace feature panel alongside Correlation Explorer, Live Stream, Charts, and Alerts. It is available from both expanded and compact navigation sidebars.

`FeaturePanelsViewModel` owns the panel lifecycle and creates a dedicated `ScheduledMessagesViewModel`. A `ScheduledMessagesView` presents list and month-calendar modes over the same filtered collection.

A focused scheduled-message management service coordinates:

- loading and updating the local index;
- resolving saved connection identities;
- creating the correct Service Bus operations instance;
- cancellation and rescheduling broker calls;
- broker-outcome recording;
- payload conversion for cloning.

The existing scheduled-message store evolves to a versioned format. New records contain:

- stable record and connection identifiers;
- connection display name and environment snapshot;
- entity and optional subscription identity;
- scheduled enqueue time and broker sequence number;
- created and last-updated timestamps;
- message identifiers and searchable metadata;
- lifecycle status and broker-confirmation details;
- an encrypted full message snapshot.

Connection strings, Azure tokens, and credentials are never stored in the index.

## Compatibility and Local State

Existing index records remain readable. Because legacy entries do not contain connection identity or a complete message payload, the console labels them as limited local records.

A legacy record may be cancelled when its connection and entity can be resolved unambiguously. Exact cloning and rescheduling remain disabled when the original payload is unavailable.

The console derives presentation states without overstating broker knowledge:

- **Upcoming (local):** the indexed enqueue time is in the future.
- **Due / unverified (local):** the enqueue time has passed, but the broker outcome is unknown.
- **Cancelled (broker confirmed):** cancellation completed successfully.
- **Rescheduled (broker confirmed):** cancellation and replacement scheduling completed successfully.
- **Action failed:** the last broker request failed.
- **Limited / stale:** required payload, connection, or entity context is unavailable.
- **Resolved locally:** the user dismissed the obsolete index entry without claiming broker cancellation.

Time passing alone never creates a broker-confirmed state.

## User Experience

### Discovery

The list view supports:

- free-text search over connection, entity, message ID, correlation ID, subject, body, and application-property keys and values;
- connection, entity, environment, lifecycle state, and time-range filters;
- clear local-index and broker-confirmation badges;
- refresh and stale-resolution actions.

The month-calendar view groups the same filtered entries by scheduled date and provides a compact daily summary with entry selection.

### Clone

Clone opens the existing send workflow prefilled from the encrypted message snapshot. It generates a new message workflow and clears the original scheduled enqueue time. Legacy records without a full snapshot cannot be cloned exactly and explain the limitation in the UI.

### Cancel

Cancellation requires a confirmation that displays:

- connection and namespace;
- environment;
- entity;
- scheduled time;
- broker sequence number.

Production destinations require an additional explicit acknowledgment. The index is updated to broker-confirmed cancelled only after the broker call succeeds.

### Reschedule

Rescheduling requires a valid future time and the same destination confirmation used for cancellation.

The service:

1. cancels the old broker sequence;
2. records confirmed cancellation;
3. schedules the stored full payload at the new time;
4. records the new sequence as a linked replacement.

If cancellation succeeds but replacement scheduling fails, the original entry remains broker-confirmed cancelled and the UI reports a partial failure. It never claims that rescheduling completed.

### Refresh and Resolve

Refresh reloads the local index, re-resolves saved connections and entity context, and recomputes derived time states. It does not claim to verify individual schedules with Azure Service Bus.

Resolve dismisses an obsolete local entry and records a local-only resolution. It does not invoke broker cancellation and cannot produce a broker-confirmed label.

## Scheduling Data Flow

Both Send Message and scheduled Replay use the same enriched-index writer.

1. The schedule request succeeds at Azure Service Bus.
2. BusLane receives the scheduled sequence number.
3. BusLane encrypts the complete payload snapshot.
4. BusLane persists the enriched local record.
5. If indexing fails, the broker success remains the primary result and the user receives a clear indexing warning.

This ordering prevents local records from claiming a broker schedule that never succeeded.

## Security and Reliability

- Full message snapshots are encrypted through `IEncryptionService`.
- Index files retain owner-only permissions and use secure atomic writes.
- Store mutations are serialized to prevent lost updates.
- Cancellation tokens propagate through store, connection-resolution, broker, and refresh operations.
- Expected cancellation does not appear as an application error.
- Corrupt or undecryptable records become visible stale entries where enough non-sensitive metadata can be recovered; the store does not silently present them as valid.
- Logs contain record, connection, entity, and sequence identifiers but never payloads, credentials, or tokens.

## Error Handling

The UI reports broker and local-index outcomes separately.

- Broker failure leaves the prior confirmed state unchanged and records the failed attempt.
- Index persistence failure after a broker success shows the broker success plus a local-index warning.
- Missing connections or ambiguous legacy matches produce stale/limited states and disable unsafe actions.
- Partial reschedule failure explicitly reports that the original schedule was cancelled but the replacement was not created.
- Decryption failure disables payload-dependent actions and offers local resolution.

## Testing

Store and serialization tests cover:

- legacy migration;
- encrypted payload round-trips;
- secure file permissions;
- concurrent add/update/resolve mutations;
- corrupted and undecryptable payload handling;
- cancellation propagation.

Management-service and view-model tests cover:

- saved-connection and entity resolution;
- search and all filters;
- derived local states;
- legacy action limitations;
- production acknowledgment;
- cancellation requests and confirmed-state updates;
- successful rescheduling and replacement linkage;
- cancellation-success/scheduling-failure partial outcomes;
- stale refresh and local resolution;
- clone request creation.

View contract tests cover:

- expanded and compact navigation entries;
- panel visibility and lifecycle;
- list/calendar switching;
- filter controls;
- local-versus-broker labels;
- confirmation content and production warnings.

Final verification runs focused tests, the full test suite, and `dotnet build`.

## Non-Goals

- Discovering scheduled messages that were not scheduled through BusLane.
- Claiming broker verification based only on queue-level scheduled counts.
- Recovering full payloads from legacy index previews.
- Bulk cancellation or bulk rescheduling.
- Cross-device synchronization of the local index.
