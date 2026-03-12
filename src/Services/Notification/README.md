# Notification Service

## Responsibility
- Notification orchestration and fan-out pipeline.

## Planned modules
- `Domain`: notification contracts.
- `Application`: routing/dispatch workflows.
- `Infrastructure`: provider adapters and queue consumers.
- `Api`: REST + gRPC endpoints.

## Data ownership
- Primarily event-driven; optional durable store in later phases.
