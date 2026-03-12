# Disaster Recovery Policy (single-region)

## RPO / RTO targets
- PostgreSQL: RPO <= 15 min, RTO <= 60 min
- Kafka: RPO <= 5 min, RTO <= 45 min
- MongoDB: RPO <= 15 min, RTO <= 60 min
- Redis: RPO <= 30 min, RTO <= 30 min
- MinIO: RPO <= 15 min, RTO <= 60 min

## Operational policy
1. Nightly full backups + periodic incremental backups.
2. Weekly backup restore rehearsal in isolated namespace.
3. Monthly DR simulation (cluster-level failure scenarios).
4. Document every DR event in incident timeline.

## Validation checklist
- Backup artifacts visible in object storage.
- Restore scripts tested and versioned.
- Secrets for backup jobs rotated quarterly.
- Alerting configured for backup failures.
