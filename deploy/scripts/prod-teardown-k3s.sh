#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="${REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
LOG_FILE="${LOG_FILE:-}"
REMOVE_LINKERD="${REMOVE_LINKERD:-false}"
REMOVE_K3S="${REMOVE_K3S:-false}"
DOMAIN="${DOMAIN:-ghjc.ru}"
STATEFUL_STORAGE_SED='s/100Gi/4Gi/g; s/200Gi/4Gi/g; s/50Gi/4Gi/g'

init_log() {
  if [[ -z "$LOG_FILE" ]]; then
    LOG_FILE="${REPO_ROOT}/prod-teardown-$(date +%Y%m%d-%H%M%S).log"
  fi
  mkdir -p "$(dirname "$LOG_FILE")"
  echo "[$(date -Iseconds)] [INFO] Log: $LOG_FILE" | tee -a "$LOG_FILE" >&2
}

log() {
  local level="${1:-INFO}"
  shift
  local msg="[$(date -Iseconds)] [$level] $*"
  echo "$msg" >> "$LOG_FILE"
  if [[ "$level" == "ERROR" ]] || [[ "$level" == "STEP" ]]; then
    echo "$msg" >&2
  fi
}

run_ignore() {
  log "CMD" "$*"
  "$@" >> "$LOG_FILE" 2>&1 || true
}

phase9_services() {
  log "STEP" "Phase 9 reverse: Remove Helm services (urfu-prod)"
  local services=(api-gateway media-service user-service chat-service presence-service notification-service call-service frontend-web)
  for svc in "${services[@]}"; do
    helm uninstall "$svc" -n urfu-prod 2>/dev/null || true
  done
}

phase8_stateful() {
  log "STEP" "Phase 8 reverse: Remove stateful stack"
  kubectl kustomize "$REPO_ROOT/deploy/k8s/platform/stateful" | sed -e "$STATEFUL_STORAGE_SED" | kubectl delete -f - --ignore-not-found --wait=false 2>/dev/null || true
  run_ignore kubectl delete pvc -n urfu-platform --all --ignore-not-found --wait=false
}

phase7b_headlamp() {
  log "STEP" "Phase 7b reverse: Remove Headlamp"
  helm uninstall headlamp -n urfu-platform 2>/dev/null || true
}

phase7_platform() {
  log "STEP" "Phase 7 reverse: Remove platform manifests (Ingress, identity, etc.)"
  run_ignore bash -c "kubectl kustomize \"$REPO_ROOT/deploy/k8s/platform\" | sed \"s/urfu-link\.local/$DOMAIN/g\" | kubectl delete -f - --ignore-not-found --wait=false"
  run_ignore kubectl delete -f "$REPO_ROOT/deploy/k8s/platform/namespaces.yaml" --ignore-not-found --wait=false
}

phase6_operators() {
  log "STEP" "Phase 6 reverse: Remove stateful operators"
  helm uninstall minio-operator -n minio-operator 2>/dev/null || true
  helm uninstall strimzi-kafka -n kafka 2>/dev/null || true
  helm uninstall redis-operator -n redis-operator 2>/dev/null || true
  helm uninstall community-operator -n mongodb 2>/dev/null || true
  helm uninstall cnpg -n cnpg-system 2>/dev/null || true
  run_ignore kubectl delete namespace minio-operator kafka redis-operator mongodb cnpg-system --ignore-not-found --wait=false --timeout=60s
}

phase5_eso() {
  log "STEP" "Phase 5 reverse: Remove External Secrets Operator"
  helm uninstall external-secrets -n external-secrets 2>/dev/null || true
  run_ignore kubectl delete namespace external-secrets --ignore-not-found --wait=false --timeout=60s
}

phase4_argocd() {
  log "STEP" "Phase 4 reverse: Remove Argo Rollouts and Argo CD"
  kubectl delete -n argo-rollouts -f https://github.com/argoproj/argo-rollouts/releases/latest/download/install.yaml --ignore-not-found --wait=false 2>/dev/null || true
  kubectl delete -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml --ignore-not-found --wait=false 2>/dev/null || true
  run_ignore kubectl delete namespace argo-rollouts argocd --ignore-not-found --wait=false --timeout=120s
}

phase3_linkerd() {
  if [[ "$REMOVE_LINKERD" != "true" ]]; then
    log "INFO" "Phase 3 reverse: Linkerd skipped (REMOVE_LINKERD != true)"
    return 0
  fi
  log "STEP" "Phase 3 reverse: Remove Linkerd Viz and Linkerd"
  if command -v linkerd &>/dev/null; then
    export PATH="${PATH}:${HOME}/.linkerd2/bin"
    linkerd viz uninstall | kubectl delete -f - --ignore-not-found --wait=false 2>/dev/null || true
    linkerd uninstall | kubectl delete -f - --ignore-not-found --wait=false 2>/dev/null || true
    linkerd uninstall --crds | kubectl delete -f - --ignore-not-found --wait=false 2>/dev/null || true
  fi
  run_ignore kubectl delete namespace linkerd-viz linkerd --ignore-not-found --wait=false --timeout=60s
}

phase2_ingress_certmanager() {
  log "STEP" "Phase 2 reverse: Remove cert-manager and ingress-nginx"
  sed "s/urfu-link\.local/$DOMAIN/g" "$REPO_ROOT/deploy/k8s/platform/cert-manager/cluster-issuer.yaml" | kubectl delete -f - --ignore-not-found 2>/dev/null || true
  helm uninstall cert-manager -n cert-manager 2>/dev/null || true
  helm uninstall ingress-nginx -n ingress-nginx 2>/dev/null || true
  run_ignore kubectl delete namespace cert-manager ingress-nginx --ignore-not-found --wait=false --timeout=60s
}

phase1_storage_k3s() {
  log "STEP" "Phase 1 reverse: Remove StorageClass and optionally k3s"
  run_ignore kubectl delete -f "$REPO_ROOT/deploy/k8s/platform/stateful/storageclass-fast-ssd.yaml" --ignore-not-found
  if [[ "$REMOVE_K3S" == "true" ]]; then
    if [[ -f /usr/local/bin/k3s-uninstall.sh ]]; then
      log "INFO" "Uninstalling k3s"
      /usr/local/bin/k3s-uninstall.sh 2>/dev/null || true
    fi
  fi
}

main() {
  init_log
  log "INFO" "Start teardown REMOVE_LINKERD=$REMOVE_LINKERD REMOVE_K3S=$REMOVE_K3S"
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  if ! kubectl cluster-info &>/dev/null; then
    log "ERROR" "Cannot reach cluster (KUBECONFIG=$KUBECONFIG). Skip app/operator removal."
    phase1_storage_k3s
    log "INFO" "End (cluster unreachable)"
    exit 0
  fi
  phase9_services
  phase8_stateful
  phase7b_headlamp
  phase7_platform
  phase6_operators
  phase5_eso
  phase4_argocd
  phase3_linkerd
  phase2_ingress_certmanager
  phase1_storage_k3s
  log "INFO" "End OK"
}

main "$@"
