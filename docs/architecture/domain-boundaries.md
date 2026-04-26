# Domain Boundaries

## User Service
- Account profile, auth claims projection, user preferences

## Media Service
- Media metadata, upload lifecycle, MinIO object references

## Chat Service
- Message streams, channels, history indexing

## Presence Service
- Online status, heartbeat, short-lived activity state

## Notification Service
- Delivery orchestration and notification fan-out

## Call Service
- Signaling workflows and LiveKit session orchestration

## Discipline Service
- Disciplines (учебные курсы), enrollments, owner-teacher invariant.
- Source of truth for discipline membership and roles (teacher/student).
- Publishes integration events that drive group-conversation lifecycle in ChatService.

## Cross-cutting ownership
- API contracts and integration events: `BuildingBlocks/Contracts`
- Security and telemetry defaults: `BuildingBlocks/Auth` + `BuildingBlocks/Observability`
- Idempotency and outbox patterns: `BuildingBlocks/Idempotency` + `BuildingBlocks/Outbox`
