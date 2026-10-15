# BusLane

<!-- impeccable:product-schema 1 -->

## Platform

web

This record covers the GitHub Pages website for BusLane, a desktop application for macOS, Windows, and Linux.

## Users

Developers evaluating an Azure Service Bus management tool. Confirmed website goal: help developers understand BusLane and download it.

## Product Purpose

BusLane brings Azure Service Bus connections, entities, message operations, and monitoring into a cross-platform desktop workspace built with Avalonia and .NET 10.

## Capabilities and Constraints

Repository README and docs/servicebus-parity-guide.md establish Azure Identity and connection-string authentication; queues, topics, subscriptions, and sessions; peek, receive-lock settlement, deferred receive, send and schedule, dead-letter resend, purge, search, and filtering; monitoring and alerts; non-destructive namespace topology tools; encrypted connection storage and optional app lock.

The scheduled-message console indexes messages scheduled through BusLane only. Local schedule states do not verify broker existence. Peeking is read-only; settlement requires receiving locked messages.

## Evidence on Hand

Real product screenshots live in docs/pics/. README.md contains installation, Azure RBAC prerequisites, usage, contribution, and MIT license information. GitHub releases provide downloads. No customer testimonials or performance benchmarks are supplied.

## Brand Commitments

Preserve BusLane name and existing app icon. Website is static HTML deployed from docs/ through GitHub Pages. User selected direct implementation in code with real screenshots.

## Product Principles

- Explain actual tasks and relevant limits clearly.
- Let real product screenshots demonstrate capabilities.
- Keep downloads, source, and setup guidance easy to find.
- Preserve keyboard access, readable contrast, and mobile usability.
