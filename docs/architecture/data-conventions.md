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
  - `user-avatars` — public, owned by UserService for profile pictures
  - `media-private` — default for chat attachments, course materials, voice messages
  - `media-public` — anonymous read-only, used for discipline cover images and
    other publicly shareable assets uploaded via MediaService
- Access model:
  - **Private assets**: download requires either ownership or an explicit
    `media_access_grants` row (owner OR direct grant OR membership-derived grant
    propagated from ChatService / DisciplineService — see "Media access model").
  - **Public assets**: any authenticated user, no grant required; read-only at
    the bucket level so MinIO can serve them directly when needed.
  - All upload/download URLs are short-lived presigned URLs (default TTL 15 min).
  - Bucket lifecycle/policies managed by IaC (`minio-init` job in dev,
    `external-secrets` + Helm values in prod).

## Media access model (Variant C: per-user replica)
- `media_access_grants` table stores `(asset_id, user_id, source, source_id)`.
- Source values: `Direct` (user-to-user share), `Conversation` (chat
  attachment), `Discipline` (course material).
- ChatService / DisciplineService push the current set of permitted users
  via the `InternalApi.GrantAssetAccess` gRPC. Membership deltas (join/leave)
  are propagated either via the same gRPC or via Kafka events on
  `urfu.chat.events.v1` / `urfu.discipline.events.v1`, keeping MediaService's
  copy eventually consistent without a synchronous fan-out on the download
  hot path.
- Cascade cleanup: when a conversation/discipline is archived the source
  service calls `RevokeAllForSource`, which performs a single
  `ExecuteDeleteAsync` on all rows matching the source.

## Soft delete + retention
- `DELETE /media/{id}` sets `deleted_at_utc` and emits
  `MediaAssetDeletedEvent`; the asset is no longer downloadable.
- `RetentionWorker` (default sweep: 24h, TTL: 30 days) hard-deletes the
  underlying MinIO object and transitions the asset to `HardDeleted`,
  emitting `MediaAssetHardDeletedEvent`. All grants cascade-delete via the
  FK on `media_access_grants`.
