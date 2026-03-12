# C4 - Container View

## Edge container
- `ApiGateway` (ASP.NET 10 + YARP)
  - OIDC token validation against Keycloak
  - Route fan-out to domain services
  - Correlation and edge rate limiting

## Logic containers
- `MediaService` (PostgreSQL)
- `UserService` (PostgreSQL)
- `ChatService` (MongoDB)
- `PresenceService` (Redis)
- `NotificationService` (stateless workers)
- `CallService` (signaling + media orchestration)

## Data and integration containers
- Kafka (topics + DLQ)
- PostgreSQL, MongoDB, Redis, MinIO
- OpenTelemetry Collector + Prometheus/Grafana/Loki/Tempo stack

## Runtime communication
- North-South: HTTPS via ingress -> gateway
- East-West sync: gRPC over Linkerd mTLS
- East-West async: Kafka events
