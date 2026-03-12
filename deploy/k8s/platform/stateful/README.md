# Self-hosted stateful stack (on-prem)

Recommended operators (production):
- PostgreSQL: CloudNativePG
- MongoDB: MongoDB Community Operator
- Redis: Redis Operator (or Redis Enterprise if available)
- Kafka: Strimzi
- MinIO: MinIO Operator

This folder contains starter CR manifests for production-grade setup.
Use dedicated storage classes and backup tooling before enabling workloads.
