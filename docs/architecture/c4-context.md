# C4 - System Context

## Primary actors
- End users: Web client / Mobile client
- Operations team: SRE / platform engineers
- Internal services: media, user, chat, presence, notification, call

## External systems and platform components
- Keycloak (identity provider)
- LiveKit + Coturn (real-time media)
- Kafka (event bus)
- PostgreSQL / MongoDB / Redis / MinIO (state)
- On-prem Kubernetes platform with Linkerd mesh

## Context boundaries
- Clients call API Gateway only.
- API Gateway handles edge concerns (auth, routing, rate limiting, correlation).
- Internal sync requests use gRPC.
- Internal async communication uses Kafka topics.
