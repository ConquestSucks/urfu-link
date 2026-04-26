# Eventing Conventions

## Transport
- Kafka for async integration events.
- gRPC for sync internal calls.

## Topic naming
`urfu.{domain}.events.v{major}`

Current baseline:
- `urfu.user.events.v1`
- `urfu.media.events.v1`
- `urfu.chat.events.v1`
- `urfu.presence.events.v1`
- `urfu.notification.events.v1`
- `urfu.call.events.v1`
- `urfu.discipline.events.v1`

## Envelope
Use `IntegrationEnvelope<TPayload>` with:
- `MessageId`
- `TraceId`
- `Source`
- `CreatedAtUtc`
- `Payload`

## Retry & DLQ
- Consumer-side retry with bounded attempts.
- DLQ topic naming: `{topic}.dlq`.
- Alert on DLQ growth and consumer lag.

## Schema strategy
- Start with JSON contracts in v1.
- Keep backward compatibility at least one minor release.
- Introduce schema registry/Avro when contract churn increases.

## Discipline events (`urfu.discipline.events.v1`)
Producer: `discipline-service`. Consumers: `chat-service` (group conversation
lifecycle), reserved for `notification-service` enrolment notifications.

| EventType | Payload (key fields) | Effect on consumers |
|---|---|---|
| `discipline.created.v1` | DisciplineId, Code, Title, Semester, OwnerTeacherId, CoverAssetId | ChatService opens a deterministic group conversation `discipline:{guid:N}` and seeds the owner as Teacher. |
| `discipline.updated.v1` | same shape as created | Reserved for downstream display refresh; no-op in current ChatService. |
| `discipline.deleted.v1` | DisciplineId | ChatService archives the conversation. |
| `discipline.user_enrolled.v1` | DisciplineId, UserId, Role, EnrolledBy | ChatService adds the participant with the role mapping. |
| `discipline.user_unenrolled.v1` | DisciplineId, UserId | ChatService removes the participant. |
| `discipline.enrollment_role_changed.v1` | DisciplineId, UserId, OldRole, NewRole | ChatService rewrites the participant role. |

Idempotency: ChatService dedups by `MessageId` via Redis SET NX (24 h TTL).
Out-of-order arrivals (e.g. `user_enrolled` before `created`) are dropped
silently — the discipline-side ordering guarantees the Created event is
emitted first; replays simply re-trigger the same final state.
