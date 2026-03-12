#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${DOMAIN:-ghjc.ru}"
REPO_ROOT="${REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
LOG_FILE="${LOG_FILE:-}"
SKIP_VAULT="${SKIP_VAULT:-false}"
INSTALL_LINKERD="${INSTALL_LINKERD:-false}"
DISK_MIN_GB=100
RAM_MIN_MB=3500
STATEFUL_STORAGE_SED='s/100Gi/4Gi/g; s/200Gi/4Gi/g; s/50Gi/4Gi/g'

init_log() {
  if [[ -z "$LOG_FILE" ]]; then
    LOG_FILE="${REPO_ROOT}/prod-bootstrap-$(date +%Y%m%d-%H%M%S).log"
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

run_cmd() {
  log "CMD" "$*"
  "$@" >> "$LOG_FILE" 2>&1
}

run_cmd_visible() {
  log "CMD" "$*"
  "$@" 2>&1 | tee -a "$LOG_FILE"
}

progress_bar() {
  local current="$1" total="$2" label="${3:-}"
  local pct=0
  [[ "$total" -gt 0 ]] && pct=$((current * 100 / total))
  local filled=$((pct / 5)) empty=$((20 - filled))
  [[ "$filled" -lt 0 ]] && filled=0
  [[ "$empty" -lt 0 ]] && empty=0
  local bar_fill="" bar_empty=""
  [[ "$filled" -gt 0 ]] && bar_fill=$(printf '#%.0s' $(seq 1 "$filled" 2>/dev/null))
  [[ "$empty" -gt 0 ]] && bar_empty=$(printf ' %.0s' $(seq 1 "$empty" 2>/dev/null))
  printf "\r  %s [%s%s] %d/%d (%d%%)  " "$label" "$bar_fill" "$bar_empty" "$current" "$total" "$pct"
  [[ "$current" -eq "$total" ]] && echo
}

spinner_pid=""
spinner_start() {
  local msg="${1:-Waiting...}"
  (
    local i=0
    local chars='-\|/'
    while true; do
      printf "\r  %s %s  " "$msg" "${chars:i++%4:1}"
      sleep 1
    done
  ) &
  spinner_pid=$!
}

spinner_stop() {
  [[ -n "${spinner_pid:-}" ]] && kill "$spinner_pid" 2>/dev/null || true
  spinner_pid=""
  printf "\r%-50s\n" "  Done."
}

phase0_env() {
  log "STEP" "Phase 0: Environment check"
  if [[ "$(id -u)" -ne 0 ]] && ! command -v sudo &>/dev/null; then
    log "ERROR" "Run as root or with sudo"
    exit 1
  fi
  local run=""
  [[ "$(id -u)" -ne 0 ]] && run="sudo"
  if ! command -v curl &>/dev/null; then
    log "ERROR" "curl required"
    exit 1
  fi
  local avail_gb
  avail_gb=$(df -BG "$REPO_ROOT" 2>/dev/null | awk 'NR==2 {gsub(/G/,""); print $4}')
  if [[ -n "$avail_gb" ]] && [[ "$avail_gb" -lt "$DISK_MIN_GB" ]]; then
    log "ERROR" "Low disk: ${avail_gb}GB (min ${DISK_MIN_GB}GB)"
    exit 1
  fi
  log "INFO" "Domain=$DOMAIN SkipVault=$SKIP_VAULT InstallLinkerd=$INSTALL_LINKERD"
}

phase1_host_and_k3s() {
  log "STEP" "Phase 1: Host preparation and k3s"
  local run=""
  [[ "$(id -u)" -ne 0 ]] && run="sudo"

  log "INFO" "apt update && upgrade"
  $run apt-get update -qq && env DEBIAN_FRONTEND=noninteractive $run apt-get upgrade -y -qq
  $run apt-get install -y -qq curl ca-certificates apt-transport-https gnupg

  local mem_mb
  mem_mb=$(awk '/MemTotal/ {print int($2/1024)}' /proc/meminfo 2>/dev/null || echo 0)
  if [[ "$mem_mb" -lt "$RAM_MIN_MB" ]]; then
    log "INFO" "RAM ${mem_mb}MB (recommended >= ${RAM_MIN_MB}MB)"
  fi

  if command -v swapoff &>/dev/null && [[ -e /proc/swaps ]]; then
    log "INFO" "Disabling swap"
    $run swapoff -a 2>/dev/null || true
    if grep -q '^[^#].*swap' /etc/fstab 2>/dev/null; then
      $run sed -i '/swap/s/^/#/' /etc/fstab
    fi
  fi

  if ! command -v k3s &>/dev/null; then
    log "INFO" "Installing k3s (Traefik disabled)"
    run_cmd_visible curl -sfL https://get.k3s.io | $run sh -s - --disable traefik
    if [[ "$(id -u)" -ne 0 ]]; then
      $run chmod 644 /etc/rancher/k3s/k3s.yaml
    fi
  fi
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

  spinner_start "Waiting for k3s node"
  for i in {1..60}; do
    if kubectl get nodes --no-headers 2>/dev/null | grep -q Ready; then
      spinner_stop
      log "INFO" "k3s node Ready"
      break
    fi
    [[ $i -eq 60 ]] && { spinner_stop; log "ERROR" "k3s node not ready"; exit 1; }
    sleep 5
  done

  if ! command -v helm &>/dev/null; then
    log "INFO" "Installing Helm"
    run_cmd_visible curl -sfL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
  fi

  if ! kubectl get storageclass fast-ssd &>/dev/null; then
    log "INFO" "Creating StorageClass fast-ssd"
    kubectl apply -f "$REPO_ROOT/deploy/k8s/platform/stateful/storageclass-fast-ssd.yaml"
  fi
}

phase2_ingress_certmanager() {
  log "STEP" "Phase 2: Ingress NGINX and cert-manager"
  kubectl create namespace ingress-nginx --dry-run=client -o yaml | kubectl apply -f -
  if ! helm status ingress-nginx -n ingress-nginx &>/dev/null; then
    helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx && helm repo update ingress-nginx
    helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n ingress-nginx --wait --timeout 5m
  fi

  kubectl create namespace cert-manager --dry-run=client -o yaml | kubectl apply -f -
  if ! helm status cert-manager -n cert-manager &>/dev/null; then
    helm repo add jetstack https://charts.jetstack.io && helm repo update jetstack
    helm upgrade --install cert-manager jetstack/cert-manager -n cert-manager \
      --set installCRDs=true --wait --timeout 5m
  fi

  sed "s/urfu-link\.local/$DOMAIN/g" "$REPO_ROOT/deploy/k8s/platform/cert-manager/cluster-issuer.yaml" | kubectl apply -f -
}

phase3_linkerd() {
  if [[ "$INSTALL_LINKERD" != "true" ]]; then
    log "INFO" "Phase 3: Linkerd skipped (INSTALL_LINKERD != true)"
    return 0
  fi
  log "STEP" "Phase 3: Linkerd + Viz"
  if ! command -v linkerd &>/dev/null; then
    run_cmd_visible curl -sL https://run.linkerd.io/install | sh
    export PATH="$PATH:$HOME/.linkerd2/bin"
  fi
  kubectl apply --server-side -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.4.0/standard-install.yaml 2>/dev/null || true
  linkerd install --crds | kubectl apply -f -
  linkerd install | kubectl apply -f -
  kubectl wait --namespace linkerd --for=condition=available deployment/linkerd-destination deployment/linkerd-identity deployment/linkerd-proxy-injector --timeout=300s
  linkerd viz install | kubectl apply -f -
  kubectl wait --namespace linkerd-viz --for=condition=available deployment/metrics-api deployment/web --timeout=300s
}

phase4_argocd() {
  log "STEP" "Phase 4: Argo CD and Argo Rollouts"
  kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply -f -
  if ! kubectl get deployment argocd-server -n argocd &>/dev/null; then
    kubectl apply --server-side --force-conflicts -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
    kubectl wait --namespace argocd --for=condition=available deployment/argocd-server --timeout=300s
  fi
  kubectl create namespace argo-rollouts --dry-run=client -o yaml | kubectl apply -f -
  kubectl apply --server-side --force-conflicts -n argo-rollouts -f https://github.com/argoproj/argo-rollouts/releases/latest/download/install.yaml 2>/dev/null || true
}

phase5_eso() {
  log "STEP" "Phase 5: External Secrets Operator"
  helm repo add external-secrets https://charts.external-secrets.io && helm repo update external-secrets
  kubectl create namespace external-secrets --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install external-secrets external-secrets/external-secrets -n external-secrets --wait --timeout 5m
  [[ "$SKIP_VAULT" == "true" ]] && log "INFO" "SKIP_VAULT=true: configure Vault and secrets later; see README"
}

phase6_operators() {
  log "STEP" "Phase 6: Stateful operators"
  local total=5 current=0

  current=1; progress_bar "$current" "$total" "CloudNativePG"
  helm repo add cnpg https://cloudnative-pg.github.io/charts && helm repo update cnpg
  kubectl create namespace cnpg-system --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install cnpg cnpg/cloudnative-pg -n cnpg-system --wait --timeout 5m

  current=2; progress_bar "$current" "$total" "MongoDB"
  helm repo add mongodb https://mongodb.github.io/helm-charts && helm repo update mongodb
  kubectl create namespace mongodb --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install community-operator mongodb/community-operator -n mongodb --wait --timeout 5m

  current=3; progress_bar "$current" "$total" "Redis"
  helm repo add ot-helm https://ot-container-kit.github.io/helm-charts && helm repo update ot-helm
  kubectl create namespace redis-operator --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install redis-operator ot-helm/redis-operator -n redis-operator --wait --timeout 5m

  current=4; progress_bar "$current" "$total" "Strimzi"
  helm repo add strimzi https://strimzi.io/charts && helm repo update strimzi
  kubectl create namespace kafka --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install strimzi-kafka strimzi/strimzi-kafka-operator -n kafka --set watchAnyNamespace=true --wait --timeout 5m

  current=5; progress_bar "$current" "$total" "MinIO"
  helm repo add minio https://operator.min.io/helm-releases && helm repo update minio
  kubectl create namespace minio-operator --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install minio-operator minio/operator -n minio-operator --wait --timeout 5m

  log "INFO" "Operators installed"
}

phase7_platform() {
  log "STEP" "Phase 7: Platform manifests (domain=$DOMAIN)"
  kubectl apply -f "$REPO_ROOT/deploy/k8s/platform/namespaces.yaml"
  kubectl kustomize "$REPO_ROOT/deploy/k8s/platform" | sed "s/urfu-link\.local/$DOMAIN/g" | kubectl apply -f -
  if [[ "$SKIP_VAULT" == "true" ]]; then
    log "INFO" "Vault skipped: create secrets manually for Keycloak and services in urfu-prod"
  fi
}

phase8_stateful() {
  log "STEP" "Phase 8: Stateful stack (120GB storage profile)"
  kubectl kustomize "$REPO_ROOT/deploy/k8s/platform/stateful" | sed -e "$STATEFUL_STORAGE_SED" | kubectl apply -f -

  spinner_start "Waiting for PostgreSQL cluster"
  for i in {1..60}; do
    if kubectl get cluster -n urfu-platform urfu-postgres -o jsonpath='{.status.phase}' 2>/dev/null | grep -qE 'Cluster in healthy state|running'; then
      spinner_stop
      break
    fi
    [[ $i -eq 60 ]] && { spinner_stop; log "INFO" "PostgreSQL wait timeout"; }
    sleep 10
  done

  spinner_start "Waiting for Kafka"
  for i in {1..60}; do
    if kubectl get kafka -n urfu-platform urfu-kafka -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null | grep -q True; then
      spinner_stop
      break
    fi
    [[ $i -eq 60 ]] && { spinner_stop; log "INFO" "Kafka wait timeout"; }
    sleep 10
  done
}

phase9_services() {
  log "STEP" "Phase 9: Deploy services (urfu-prod)"
  local services=(api-gateway media-service user-service chat-service presence-service notification-service call-service frontend-web)
  local total=${#services[@]} current=0
  for svc in "${services[@]}"; do
    ((current++)) || true
    progress_bar "$current" "$total" "Helm $svc"
    helm upgrade --install "$svc" "$REPO_ROOT/deploy/helm/charts/urfu-service" -n urfu-prod --create-namespace -f "$REPO_ROOT/deploy/helm/services/$svc/values-prod.yaml"
  done
  log "INFO" "Services deployed"
}

phase10_wait_smoke() {
  log "STEP" "Phase 10: Wait for pods and smoke"
  spinner_start "Waiting for urfu-prod pods"
  for i in {1..60}; do
    local ready total
    ready=$(kubectl get pods -n urfu-prod -l 'app.kubernetes.io/name' -o jsonpath='{.items[*].status.conditions[?(@.type=="Ready")].status}' 2>/dev/null | tr ' ' '\n' | grep -c True || echo 0)
    total=$(kubectl get pods -n urfu-prod -l 'app.kubernetes.io/name' --no-headers 2>/dev/null | wc -l)
    [[ "$total" -gt 0 ]] && [[ "$ready" -ge "$((total/2))" ]] && { spinner_stop; break; }
    [[ $i -eq 60 ]] && { spinner_stop; log "INFO" "Pods wait timeout"; }
    sleep 5
  done
  log "INFO" "Bootstrap complete. API: https://api.$DOMAIN Frontend: https://app.$DOMAIN"
}

main() {
  init_log
  log "INFO" "Start DOMAIN=$DOMAIN REPO_ROOT=$REPO_ROOT"
  phase0_env
  phase1_host_and_k3s
  phase2_ingress_certmanager
  phase3_linkerd
  phase4_argocd
  phase5_eso
  phase6_operators
  phase7_platform
  phase8_stateful
  phase9_services
  phase10_wait_smoke
  log "INFO" "End OK"
}

main "$@"
