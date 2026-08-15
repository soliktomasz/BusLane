# Service Bus Parity Workflows

## Namespace Topology

BusLane topology documents are secret-free JSON snapshots of queues, topics, subscriptions, and subscription rules. Import is non-destructive in v1: it creates missing resources and updates supported settings, but does not delete target entities.

Recommended flow:

1. Export source namespace topology.
2. Compare against target namespace.
3. Review create/update/skip actions.
4. Apply only after dry-run output matches intent.

## Receive-Lock

Peek remains read-only. Settlement actions require receiving locked messages first. Locked messages can be completed, abandoned, deferred, dead-lettered, or lock-renewed while Azure lock validity allows.

## Deferred Messages

Deferred messages are recovered by sequence number. Enter one or more sequence numbers separated by commas, spaces, or new lines, then settle returned messages from the locked-message panel.

## Scheduled Messages

The Scheduled Messages console indexes only messages scheduled through BusLane. New records keep the complete message snapshot encrypted locally; connection strings, credentials, tokens, message bodies, and application-property values are not stored as plaintext index metadata. Legacy index records remain visible, but payload-dependent clone and reschedule actions are unavailable.

Azure Service Bus does not expose an API for enumerating all scheduled messages or verifying one schedule by sequence number. Refresh therefore reloads and re-resolves the local index; it cannot prove that an individual schedule still exists at the broker. The console labels derived states such as upcoming, due, stale, and resolved as local-only. Cancelled and rescheduled labels are broker-confirmed only after the corresponding broker request succeeds.

Rescheduling cancels the original sequence before creating its replacement. If cancellation succeeds but replacement scheduling fails, the original remains broker-confirmed cancelled and the console reports a partial failure. Resolving a stale record changes only the local index and never claims broker cancellation.
