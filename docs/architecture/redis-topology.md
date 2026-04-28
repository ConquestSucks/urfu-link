# Redis Topology

A single Redis instance backs every Redis-using concern in dev and staging:

- **SignalR backplane** (`urfu:signalr:chat`, `urfu:signalr:presence`, `urfu:signalr:notifications` channel prefixes) — required so a broadcast issued on one replica reaches connections held by other replicas
- **Idempotency** (`urfu:idempotency:*` via `BuildingBlocks.Idempotency`) — used by every Kafka consumer for envelope-MessageId deduplication
- **Outbox** (`urfu:outbox` via `BuildingBlocks.Outbox`) — Redis-backed pending-events queue between the EF outbox writer and the Kafka publisher
- **Session revocation** (`urfu:session:*` via `BuildingBlocks.SessionRevocation`) — deny/allow lists consumed by ApiGateway and UserService
- **DataProtection keys** (`urfu:dp:*`) — survives pod restart and supports horizontal scaling
- **Presence sessions / typing / privacy projection** (`presence:*`, `typing:*`)
- **NotificationService badge counters** (`notif:badge:{userId}`)

This is intentional in dev/staging — the same instance keeps the local-up footprint small. **Production needs Redis Sentinel (or Redis Cluster).** A single instance is a hard SPOF: if Redis goes down,

- SignalR broadcasts cannot propagate cross-replica → users on different pods stop seeing each other's messages
- Idempotency reads fail → Kafka consumers can't decide whether an envelope is a duplicate; depending on policy they either reject (lose work) or replay (duplicate side effects)
- Outbox consumers stall → integration events accumulate in the writer side until Redis is back
- SessionRevocation reads fail → either every request is denied (hard fail-closed) or every request is allowed (silent fail-open)

## Plan

The Helm chart for the platform exposes a single `redis` Service. For prod, swap it to a Sentinel-backed StatefulSet (`bitnami/redis` with `architecture: replication` + `sentinel.enabled: true` is the standard route). Application configuration changes amount to passing the Sentinel master name as the connection string — the StackExchange.Redis client transparently follows failovers — so no service code change is required.

Tracking issue: separate infra task; this document captures the design intent. The plumbing inside the application (every concern reads from `Infrastructure:Redis:Configuration`) already supports multi-host connection strings without code changes.
