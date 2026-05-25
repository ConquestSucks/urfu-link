#!/usr/bin/env bash
set -euo pipefail

SINCE="${SINCE:-60m}"
APP_NAMESPACES="${APP_NAMESPACES:-urfu-prod urfu-platform argocd ingress-nginx observability}"
ERROR_PATTERN='MongoAuthenticationException|Authentication failed|Unable to initialize OpenID|StatusCode="Unauthenticated"|HTTP status code: 401|CrashLoopBackOff|Back-off restarting failed container'

require() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 2
  fi
}

require kubectl
require jq
require curl

echo "== ArgoCD applications =="
kubectl -n argocd get applications.argoproj.io -o json \
  | jq -r '
      .items[]
      | [.metadata.name, .status.sync.status, .status.health.status]
      | @tsv
    ' \
  | tee /tmp/urfu-argocd-apps.tsv
if awk '$2 != "Synced" || $3 != "Healthy" { bad=1 } END { exit bad ? 1 : 0 }' /tmp/urfu-argocd-apps.tsv; then
  echo "ArgoCD applications are Synced/Healthy."
else
  echo "At least one ArgoCD application is not Synced/Healthy." >&2
  exit 1
fi

echo "== Pods =="
kubectl get pods -A -o json \
  | jq -r '
      .items[]
      | select(.metadata.namespace | IN("urfu-prod","urfu-platform","argocd","ingress-nginx","observability"))
      | {
          ns: .metadata.namespace,
          name: .metadata.name,
          phase: .status.phase,
          waiting: ([.status.containerStatuses[]? | select(.state.waiting != null) | .state.waiting.reason] | join(",")),
          restarts: ([.status.containerStatuses[]?.restartCount] | add // 0),
          ready: ([.status.containerStatuses[]? | select(.ready == true)] | length),
          total: ([.status.containerStatuses[]?] | length)
        }
      | select(.phase != "Running" and .phase != "Succeeded" or .waiting != "" or .ready != .total)
      | [.ns, .name, .phase, .waiting, (.ready|tostring)+"/"+(.total|tostring), (.restarts|tostring)]
      | @tsv
    ' \
  | tee /tmp/urfu-bad-pods.tsv
if [ -s /tmp/urfu-bad-pods.tsv ]; then
  echo "Pods with bad phase/waiting containers/not-ready containers were found." >&2
  exit 1
fi
echo "Pods are ready; no CrashLoopBackOff/not-ready containers found."

echo "== Argo Rollouts =="
if kubectl get rollouts.argoproj.io -A -o json >/tmp/urfu-rollouts.json 2>/dev/null; then
  jq -r '
      .items[]
      | {
          ns: .metadata.namespace,
          name: .metadata.name,
          phase: (.status.phase // ""),
          degraded: ([.status.conditions[]? | select(.type == "Degraded" and .status == "True")] | length)
        }
      | select(.phase != "Healthy" or .degraded > 0)
      | [.ns, .name, .phase, (.degraded|tostring)]
      | @tsv
    ' /tmp/urfu-rollouts.json | tee /tmp/urfu-bad-rollouts.tsv
  if [ -s /tmp/urfu-bad-rollouts.tsv ]; then
    echo "Unhealthy or degraded rollouts were found." >&2
    exit 1
  fi
  echo "Rollouts are healthy."
else
  echo "Argo Rollouts CRD is not available; skipping rollout check."
fi

echo "== Jobs =="
kubectl get jobs -A -o json \
  | jq -r '
      .items[]
      | select(.metadata.namespace | IN("urfu-prod","urfu-platform"))
      | select((.status.failed // 0) > 0)
      | [.metadata.namespace, .metadata.name, (.status.failed|tostring), (.status.conditions[]? | select(.type == "Failed") | .reason)]
      | @tsv
    ' \
  | tee /tmp/urfu-failed-jobs.tsv
if [ -s /tmp/urfu-failed-jobs.tsv ]; then
  echo "Failed jobs were found." >&2
  exit 1
fi
echo "No failed jobs found."

echo "== Gateway downstream health =="
GATEWAY_PORT="${GATEWAY_PORT:-18080}"
kubectl -n urfu-prod port-forward svc/api-gateway "${GATEWAY_PORT}:8080" >/tmp/urfu-gateway-port-forward.log 2>&1 &
PF_PID=$!
cleanup_port_forward() {
  kill "$PF_PID" >/dev/null 2>&1 || true
}
trap cleanup_port_forward EXIT
for _ in $(seq 1 30); do
  if curl -fsS "http://127.0.0.1:${GATEWAY_PORT}/health/downstreams" >/dev/null; then
    break
  fi
  sleep 1
done
curl -fsS "http://127.0.0.1:${GATEWAY_PORT}/health/downstreams" >/dev/null
cleanup_port_forward
trap - EXIT
echo "Gateway downstream health endpoint returned 200."

echo "== Recent logs (${SINCE}) =="
for ns in $APP_NAMESPACES; do
  while IFS= read -r pod; do
    [ -n "$pod" ] || continue
    kubectl -n "$ns" logs "$pod" --all-containers --since="$SINCE" --prefix=true 2>/dev/null || true
  done < <(kubectl -n "$ns" get pods -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}' 2>/dev/null || true)
done | grep -E "$ERROR_PATTERN" | tee /tmp/urfu-recent-log-errors.txt || true
if [ -s /tmp/urfu-recent-log-errors.txt ]; then
  echo "Recent critical log patterns were found." >&2
  exit 1
fi
echo "No recent critical log patterns found."
