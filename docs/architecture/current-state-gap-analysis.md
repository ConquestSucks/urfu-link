# Current State Gap Analysis

## Summary
The repository provides a strong `prod-first` platform skeleton, but the runnable system is still at bootstrap maturity rather than production readiness. The biggest issue is not a single bug, but a consistency gap between the documented target architecture and the currently executable runtime.

## Current issues
- Local development uses `docker-compose` for dependencies and `dotnet run` for services, while production uses Helm, Kubernetes, Linkerd, GitOps, ingress, rollout objects, and namespace policies.
- Runtime building blocks still use in-memory reliability primitives for idempotency and outbox, which means retries and duplicate protection are not durable across pod restarts.
- Service code is almost entirely transport/bootstrap code. The bounded-context modules described in service READMEs are not yet represented in the codebase.
- CI builds and pushes images, but the delivery flow does not align image tagging with the Helm values consumed by deployments.
- Production security controls exist only partially. Some controls are chart-level, some are namespace-level, and several are implemented only for `urfu-prod` or only for a single service.
- Tests are mostly placeholders and do not validate the real runtime contract of gateway, services, Kubernetes manifests, or delivery flow.

## Structural risks
- Low dev/prod parity increases the chance of environment-specific defects in routing, authentication, service discovery, secrets, and network policy.
- In-memory outbox and idempotency break the reliability guarantees expected from event-driven microservices.
- Partial Linkerd and NetworkPolicy coverage creates uneven east-west trust boundaries.
- A service template without real domain/application/infrastructure boundaries will accumulate business logic directly in API projects.
- Mutable deployment conventions increase the risk of releasing the wrong image artifact.

## Target state
- One primary runtime model: local Kubernetes for development and Kubernetes for production.
- Same Helm chart family, same service discovery model, same namespace layout, same security posture, and same observability contract in `dev` and `prod`.
- Durable reliability building blocks backed by shared infrastructure rather than memory.
- Real bounded-context structure inside every service.
- CI/CD based on reproducible artifacts, stronger supply-chain guarantees, and manifest validation.
- Tests that validate both code-level contracts and cluster-facing runtime assumptions.

## Immediate implementation priorities
1. Introduce `local-k8s` as the primary development path.
2. Remove environment drift in platform manifests, service discovery, and secrets flow.
3. Replace bootstrap reliability primitives with durable implementations.
4. Refactor service layout so business logic does not live in API bootstrap code.
5. Upgrade CI/CD and test coverage to reflect the production operating model.
