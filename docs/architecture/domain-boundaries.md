# Domain Boundaries

## User Service
- Account profile, auth claims projection, user preferences

## Media Service
- Media metadata, upload lifecycle, MinIO object references

## Chat Service
- Message streams, channels, history indexing

## Presence Service
- Online status, heartbeat, short-lived activity state
- Owns the typing indicator: `InternalApi.SetTyping` is the server-to-server seam used by ChatHub.StartTyping/StopTyping after chat-side `IsParticipant` authorization, so PresenceHub clients can never poison another conversation's typing state.

### Privacy projection vs gRPC pull (design choice)

The MVP shipped a Kafka projection: UserService publishes `urfu.user.events.v1` with privacy changes, PresenceService consumes them into a Redis projection (`RedisPrivacyProjectionStore`) keyed by user id. Reads against the projection are O(1) and survive a UserService outage — the trade-off is eventual consistency between the moment a user toggles privacy and the moment the projection observes it (Kafka lag).

A gRPC pull from UserService on every request would be strongly consistent at the cost of latency and a hard runtime dependency. The `IPrivacyResolver` interface is shaped to support a hybrid (Redis-projection primary, gRPC fallback on cache-miss / stale data) but the gRPC fallback is intentionally deferred: the projection's TTL is short (60s) and a cache miss simply falls back to a permissive default, which is acceptable for the privacy contract today.

### last_seen snapshot vs last_seen_history

`presence.last_seen` is a one-row-per-user snapshot updated on every disconnect — fast PK lookups for the "show me a user's last_seen now" query. `presence.last_seen_history` is the append-only audit record of every snapshot transition, partitioned monthly on `recorded_at_utc`, retained 12 months. Analytics queries ("when was this user online during March?") run against the history table without joining Kafka logs. The write-through is atomic with the snapshot mutation (single `SaveChangesAsync`, single transaction), so the history can never lag behind.

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
