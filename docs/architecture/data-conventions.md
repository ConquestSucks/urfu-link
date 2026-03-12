# Data Conventions

## PostgreSQL
- One database/schema per service context.
- Migrations via service-local migration projects (to be added with feature implementation).
- Naming: `snake_case` tables, explicit indexes for lookup/read models.

## MongoDB
- Separate database per service (`chat_db`).
- Collection naming: singular bounded-entity names.
- TTL indexes for ephemeral collections where relevant.

## Redis
- Key format: `urfu:{service}:{aggregate}:{id}`.
- Presence keys should have TTL and be refreshed by heartbeat.

## MinIO
- Buckets:
  - `media-private`
  - `media-public`
- Access model:
  - private by default
  - presigned URL flow for uploads/downloads
  - bucket policies managed by IaC
