# Platform manifests

This folder stores on-prem Kubernetes baseline manifests:

- `namespaces.yaml` - logical separation for platform/dev/prod
- `argocd/*` - GitOps project and applicationset
- `linkerd/*` - service mesh policies and installation guidance
- `ingress/*` - north-south entrypoint
- `cert-manager/*` - certificate issuer baseline
- `external-secrets/*` - secret management integration
- `network/*` - default network isolation baseline
- `observability/*` - monitoring stack guidance
- `argo-rollouts/*` - blue/green rollout controller guidance
- `stateful/*` - self-hosted operators, topic bootstrap, and backup jobs

## Ownership boundaries
- This repository manages application-facing platform manifests and self-hosted workload definitions that are consumed by the URFU Link services.
- Cluster-wide platform controllers are intentionally not installed from this folder. Their versions should be pinned and upgraded from a separate bootstrap or infrastructure repository.
- Both the local developer stack and the Strimzi stateful manifests now target Kafka KRaft. The Kubernetes cluster bootstrap must run a Strimzi operator version that supports `KafkaNodePool`-based KRaft deployments.

## Suggested apply order
1. `kubectl apply -f deploy/k8s/platform/namespaces.yaml`
2. Install Linkerd + Linkerd Viz (see `linkerd/README.md`)
3. Install Argo CD and Argo Rollouts
4. `kubectl apply -k deploy/k8s/platform`
5. `kubectl apply -k deploy/k8s/platform/stateful`
