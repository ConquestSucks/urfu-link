# ADR 0002: gRPC for Sync and Kafka for Async

- Status: Accepted
- Date: 2026-03-01

## Context
Services must communicate with low latency for direct requests and still support decoupled event-driven workflows.

## Decision
- Use gRPC for synchronous service-to-service communication.
- Use Kafka for asynchronous integration events.
- Standardize topic naming and envelope schema in shared contracts package.

## Consequences
- Better typed contracts and lower overhead for internal sync calls.
- Event-driven workflows become resilient to temporary service downtime.
- Requires clear ownership of event schemas and versioning policy.
