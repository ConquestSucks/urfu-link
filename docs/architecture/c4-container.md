# C4 - Container View

## Edge container
- `ApiGateway` (ASP.NET 10 + YARP)
  - OIDC token validation against Keycloak
  - Route fan-out to domain services
  - Correlation and edge rate limiting

## Logic containers
- `MediaService` (PostgreSQL + MinIO) — asset metadata; presigned upload/download to MinIO
- `UserService` (PostgreSQL) — profile, JSONB notification settings, gRPC for downstream services
- `ChatService` (MongoDB + Redis) — conversations and messages in Mongo, idempotency + outbox in Redis
- `PresenceService` (Redis + PostgreSQL with `last_seen_history`) — Redis is the hot session/typing/projection store; PG keeps the partitioned long-term last-seen history for analytics
- `NotificationService` (PostgreSQL partitioned + Redis) — notifications/deliveries in PG with monthly partitions (90-day retention), badge counters in Redis
- `CallService` (signaling + media orchestration)
- `DisciplineService` (PostgreSQL) — disciplines, enrollments, gRPC for ChatService

## Data and integration containers
- Kafka (topics + DLQ)
- PostgreSQL, MongoDB, Redis, MinIO
- OpenTelemetry Collector + Prometheus/Grafana/Loki/Tempo stack

## Runtime communication
- North-South: HTTPS via ingress -> gateway
- East-West sync: gRPC over Linkerd mTLS
- East-West async: Kafka events

### DisciplineService data flow
- `ApiGateway` rewrites `/api/disciplines/...` to `discipline-service:8080/api/v1/disciplines/...`.
- `discipline-service` publishes `urfu.discipline.events.v1` via the shared Outbox.
- `chat-service` consumes the topic and projects each event onto the
  conversation backing the discipline (creates / archives the group chat,
  syncs participants and roles).
- `chat-service` resolves teacher / student membership through
  `DisciplineService.InternalApi` gRPC for non-pinning authorization paths.
