# ADR 0001: Platform Bootstrap Baseline

- Status: Accepted
- Date: 2026-03-01

## Context
A greenfield microservice platform requires a production-ready baseline before feature development starts.

## Decision
1. Monorepo with ASP.NET 10 services and shared building blocks.
2. On-prem Kubernetes as production target.
3. Self-hosted stateful components (PostgreSQL, MongoDB, Redis, Kafka, MinIO).
4. Linkerd + Linkerd Viz as service mesh baseline.
5. Blue/green release strategy via Argo Rollouts.
6. GitHub Actions + Argo CD for CI/CD and GitOps.

## Consequences
- Strong control over runtime and infrastructure posture.
- Higher ops ownership burden for stateful systems.
- Fast team onboarding via service template and standardized delivery stack.
