# ADR 0004: Blue/Green Deployments via Argo Rollouts

- Status: Accepted
- Date: 2026-03-01

## Context
Production release strategy requires low-risk promotion and fast rollback semantics.

## Decision
- Use Argo Rollouts with blue/green strategy for all deployable services.
- Auto-promotion enabled in dev and disabled in prod.
- Promotion gate controlled by Argo CD workflow and health verification.

## Consequences
- Safer deployments and explicit promotion controls.
- Requires Rollouts controller and operational runbook for promotions.
- Additional traffic-switching resources (active/preview services) are required.
