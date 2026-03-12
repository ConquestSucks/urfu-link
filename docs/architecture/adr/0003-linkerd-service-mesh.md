# ADR 0003: Linkerd as Service Mesh

- Status: Accepted
- Date: 2026-03-01

## Context
On-prem production requires service identity, mTLS, and traffic-level observability without introducing excessive operational complexity.

## Decision
- Use Linkerd as the service mesh for all production namespaces.
- Enable Linkerd Viz for operator diagnostics.
- Enforce mTLS via policy objects (`Server`, `MeshTLSAuthentication`, `ServerAuthorization`).

## Consequences
- Secure east-west communication by default.
- Better runtime visibility for latency and error budgets.
- Platform team must maintain Linkerd control-plane lifecycle.
