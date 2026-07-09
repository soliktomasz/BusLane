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

BusLane stores a local index for messages scheduled through BusLane and records Azure sequence numbers. Azure Service Bus does not expose a general API for listing every scheduled message, so the local list is not an authoritative namespace-wide schedule.
