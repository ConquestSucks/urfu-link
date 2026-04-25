# PresenceService

Real-time online status, multi-device sessions, last_seen, and typing indicators
for UrfuLink.

## Storage layout

| Where        | What                                                            |
|--------------|-----------------------------------------------------------------|
| Redis hash   | `presence:user:{userId:N}:sessions` → deviceId → JSON session, TTL 30s |
| Redis ZSET   | `presence:heartbeats` (member=`userId:deviceId`, score=unix-ms) |
| Redis string | `presence:typing:{convId:N}:{userId:N}` with TTL 5s             |
| Redis hash   | `presence:privacy:{userId:N}` projection from UserService       |
| PostgreSQL   | `presence.last_seen` (durable last_seen + `xmin` concurrency)   |

Atomic Redis ops use Lua (`AddSession` / `RemoveSession`) so connect/disconnect
is race-safe with the sweeper.

## Public surface

- **REST** (FastEndpoints, prefix `/api/v1/presence`):
  - `GET users/{userId}` — aggregated presence with privacy applied
  - `POST users/batch` — `{ userIds: Guid[] }` (max 100)
- **gRPC** (internal, no privacy filter): `Ping`, `GetPresence`,
  `GetPresenceBatch`, `IsOnline`, `IsTyping`
- **SignalR Hub** at `/hubs/presence`:
  - Client → Hub: `Heartbeat`, `SetStatus`, `StartTyping`, `StopTyping`,
    `SubscribeToUsers`, `UnsubscribeFromUsers`
  - Hub → Client: `UserPresenceChanged(userId, status, platforms[], lastSeenAt?)`,
    `UserTyping(conversationId, userId, isTyping)`
  - JWT travels in `?access_token=` for the WebSocket upgrade.
  - DeviceId resolution: claim `device_id` → query `?deviceId=` → generated GUID.
  - Platform: query `?platform=` (Mobile / Web / Desktop), default `Web`.

## Integration events → `urfu.presence.events.v1`

- `presence.user.online.v1` — first session of the user
- `presence.user.offline.v1` — last session removed (Hub disconnect or sweeper)

## Kafka subscriptions

Subscribes to `urfu.user.events.v1` (group `presence-privacy-projection-v1`) and
projects `user.privacy.changed.v1` into the local Redis privacy hash. No gRPC
roundtrip to UserService.

## Operational notes

- **Single replica only.** No SignalR backplane — scaling out would split
  presence groups. A Redis backplane is tracked as a follow-up.
- Migrations apply via the `--migrate` flag (Helm pre-install Job), same as
  MediaService.
- The dev Redis container needs `allowAdmin=true` for `FLUSHDB` between tests
  (only used by integration tests).
