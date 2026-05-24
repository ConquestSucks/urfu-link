# ApiGateway

YARP-based reverse proxy plus edge concerns: auth, rate limiting, correlation, and session revocation.

## Routing model (`appsettings.json`)

REST routes carry `"AuthorizationPolicy": "default"`, which forces the gateway to validate the JWT before forwarding. SignalR hub routes (`chat-hub`, `presence-hub`, `notification-hub`) intentionally omit the `AuthorizationPolicy` field. This is by design:

- SignalR clients cannot set the `Authorization` HTTP header during the WebSocket upgrade handshake, so browser WebSocket traffic may carry the JWT via the `?access_token=...` query parameter.
- HTTP negotiate requests may carry `Authorization: Bearer ...`.
- Pomerium-protected production traffic carries the signed identity assertion in the configured edge header, `X-Pomerium-Jwt-Assertion`.
- The gateway therefore relies on two layers of defence-in-depth for hub traffic: `HubAccessTokenPresenceMiddleware` rejects connections that omit all accepted token carriers, and the downstream service still validates the JWT before letting the connection complete the handshake.

If you need to add a new hub route, follow the same pattern: drop `AuthorizationPolicy`, leave the path under `/hubs/`, and ensure the downstream service can read the same token carriers used by the deployed edge.

## Rate limiting

| Policy | Scope | Limit |
|---|---|---|
| Global | All routes | 200 rps per partition (Keycloak `sub` or remote IP) |
| `chat-send` | `POST /api/chat/conversations/{id}/messages` | 60 / minute / partition |
| `media-upload` | `POST /api/media/upload/init` | 30 / minute / partition |

Search endpoints implement their own per-user limit at the service level (see `ChatSearchRateLimiterPolicy` in ChatService).

## Health checks

- `/health/live` - always 200 unless the process is dead. Wired to kubelet liveness; do not chain it to YARP destinations.
- `/health/ready` - process readiness for kubelet. It intentionally does not aggregate downstream service health, so one broken service does not remove every gateway route from Kubernetes endpoints.
- `/health/downstreams` - aggregates the `yarp-destinations` check (active YARP probes against each cluster's `/health/ready`) for operational diagnostics.
