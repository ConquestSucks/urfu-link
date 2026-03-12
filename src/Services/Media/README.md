# Media Service

## Responsibility
- Media metadata and processing orchestration.
- Object storage references in MinIO.

## Planned modules
- `Domain`: media aggregate and policies.
- `Application`: commands/queries and validation.
- `Infrastructure`: PostgreSQL + MinIO adapters.
- `Api`: REST + gRPC endpoints.

## Data ownership
- PostgreSQL schema: `media`
- MinIO buckets: `media-private`, `media-public`
