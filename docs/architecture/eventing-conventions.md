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
