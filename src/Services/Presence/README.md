# Presence Service

## Responsibility
- Real-time presence state and short-lived activity markers.

## Planned modules
- `Domain`: presence policy model.
- `Application`: heartbeat workflows.
- `Infrastructure`: Redis adapters.
- `Api`: REST + gRPC endpoints.

## Data ownership
- Redis keys with prefix: `urfu:presence:*`
