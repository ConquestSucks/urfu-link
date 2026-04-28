# ApiGateway

YARP-based reverse proxy + edge concerns (auth, rate limiting, correlation, session revocation).

## Routing model (`appsettings.json`)

REST routes carry `"AuthorizationPolicy": "default"`, which forces the gateway to validate the JWT (Keycloak realm via `Auth:Authority`) before forwarding. SignalR hub routes (`chat-hub`, `presence-hub`, `notification-hub`) intentionally **omit** the `AuthorizationPolicy` field. This is **by design**:

- SignalR clients cannot set the `Authorization` HTTP header during the WebSocket upgrade handshake (browsers strip custom headers from the upgrade), so the JWT travels via the `?access_token=...` query parameter.
- Each downstream SignalR service has a `JwtBearerEvents.OnMessageReceived` hook that promotes that query parameter into the bearer token before the auth middleware runs.
- The gateway therefore relies on **two layers of defence-in-depth** for hub traffic: `HubAccessTokenPresenceMiddleware` rejects connections that omit `?access_token=` (so anonymous WebSocket attempts fail at the edge), and the downstream service still validates the JWT before letting the connection complete the handshake.

If you need to add a new hub route, follow the same pattern: drop `AuthorizationPolicy`, leave the path under `/hubs/`, and ensure the downstream service's `Program.cs` configures `JwtBearerEvents.OnMessageReceived` to read `access_token` from the query string.

## Rate limiting

| Policy | Scope | Limit |
|---|---|---|
| Global | All routes | 200 rps per partition (Keycloak `sub` or remote IP) |
| `chat-send` | `POST /api/chat/conversations/{id}/messages` | 60 / minute / partition |
| `media-upload` | `POST /api/media/upload/init` | 30 / minute / partition |

Search endpoints implement their own per-user limit at the service level (see `ChatSearchRateLimiterPolicy` in ChatService).

## Health checks

- `/health/live` — always 200 unless the process is dead. Wired to kubelet liveness; do **not** chain it to YARP destinations.
- `/health/ready` — aggregates the `yarp-destinations` check (active YARP probes against each cluster's `/health/ready`). Wired to kubelet readiness so a degraded downstream takes the gateway pod out of rotation without restarting it.
