#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${DOMAIN:-ghjc.ru}"
REPO_ROOT="${REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
LOG_FILE="${LOG_FILE:-}"
SKIP_VAULT="${SKIP_VAULT:-false}"
INSTALL_LINKERD="${INSTALL_LINKERD:-true}"
LINKERD2_VERSION="${LINKERD2_VERSION:-edge-25.10.7}"
DISK_MIN_GB=90
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

helm_repo_add_update() {
  local name="$1" repo_url="$2" attempt=1 max=3 add_ok update_ok
  while true; do
    helm repo add "$name" "$repo_url" 2>&1 | tee -a "$LOG_FILE"
    add_ok=${PIPESTATUS[0]}
    helm repo update "$name" 2>&1 | tee -a "$LOG_FILE"
    update_ok=${PIPESTATUS[0]}
    [[ $add_ok -eq 0 ]] && [[ $update_ok -eq 0 ]] && return 0
    [[ $attempt -ge $max ]] && { log "ERROR" "helm repo add/update $name failed after $max attempts"; return 1; }
    log "INFO" "helm repo $name attempt $attempt failed (add=$add_ok update=$update_ok), retry in 15s..."
    sleep 15
    attempt=$((attempt + 1))
  done
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

  log "INFO" "Waiting for k3s node..."
  for i in {1..60}; do
    if kubectl get nodes --no-headers 2>/dev/null | grep -q Ready; then
      log "INFO" "k3s node Ready"
      break
    fi
    [[ $i -eq 60 ]] && { log "ERROR" "k3s node not ready"; exit 1; }
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

wait_ns_ready() {
  local ns="$1"
  for i in {1..60}; do
    local phase
    phase=$(kubectl get namespace "$ns" -o jsonpath='{.status.phase}' 2>/dev/null || echo "")
    if [[ -z "$phase" ]] || [[ "$phase" == "Active" ]]; then
      return 0
    fi
    [[ $i -eq 60 ]] && { log "INFO" "Timeout waiting for namespace $ns"; return 1; }
    sleep 5
  done
}

phase2_ingress_certmanager() {
  log "STEP" "Phase 2: Ingress NGINX and cert-manager"
  wait_ns_ready ingress-nginx || true
  wait_ns_ready cert-manager || true
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
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  if ! command -v linkerd &>/dev/null; then
    run_cmd_visible env LINKERD2_VERSION="$LINKERD2_VERSION" curl -sL https://run.linkerd.io/install-edge | sh
    export PATH="$PATH:$HOME/.linkerd2/bin"
  fi
  kubectl apply --server-side -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.4.0/standard-install.yaml 2>/dev/null || true
  linkerd install --crds | kubectl apply -f -
  if linkerd upgrade | kubectl apply -f - 2>/dev/null; then
    log "INFO" "Linkerd control plane upgraded"
  elif kubectl get configmap linkerd-config -n linkerd &>/dev/null; then
    log "INFO" "Linkerd control plane already installed, skipping install"
  else
    log "INFO" "Linkerd not installed, running install"
    linkerd install | kubectl apply -f -
  fi
  kubectl wait --namespace linkerd --for=condition=available deployment/linkerd-destination deployment/linkerd-identity deployment/linkerd-proxy-injector --timeout=300s
  if linkerd viz upgrade | kubectl apply -f - 2>/dev/null; then
    log "INFO" "Linkerd Viz upgraded"
  elif kubectl get deployment metrics-api -n linkerd-viz &>/dev/null; then
    log "INFO" "Linkerd Viz already installed, skipping install"
  else
    log "INFO" "Linkerd Viz not installed, running install"
    linkerd viz install | kubectl apply -f -
  fi
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
  helm_repo_add_update external-secrets https://charts.external-secrets.io
  kubectl create namespace external-secrets --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install external-secrets external-secrets/external-secrets -n external-secrets --wait --timeout 5m
  [[ "$SKIP_VAULT" == "true" ]] && log "INFO" "SKIP_VAULT=true: configure Vault and secrets later; see README"
  log "INFO" "Phase 5 done"
}

phase6_install_one() {
  local name="$1" repo_url="$2" chart="$3" ns="$4" release_name="${5:-$1}" helm_extra="${6:-}" helm_ret
  log "CMD" "helm repo add $name $repo_url && helm repo update $name"
  helm_repo_add_update "$name" "$repo_url"
  kubectl create namespace "$ns" --dry-run=client -o yaml | kubectl apply -f -
  log "INFO" "Installing $release_name in $ns..."
  helm upgrade --install "$release_name" "$chart" -n "$ns" $helm_extra --wait --timeout 5m 2>&1 | tee -a "$LOG_FILE"
  helm_ret=${PIPESTATUS[0]}
  if [[ $helm_ret -ne 0 ]]; then
    log "ERROR" "helm upgrade --install $release_name failed (exit $helm_ret)"
    exit 1
  fi
}

phase6_operators() {
  log "STEP" "Phase 6: Stateful operators"
  phase6_install_one cnpg https://cloudnative-pg.github.io/charts cnpg/cloudnative-pg cnpg-system
  phase6_install_one mongodb https://mongodb.github.io/helm-charts mongodb/community-operator mongodb community-operator
  phase6_install_one ot-helm https://ot-container-kit.github.io/helm-charts ot-helm/redis-operator redis-operator redis-operator
  phase6_install_one strimzi https://strimzi.io/charts strimzi/strimzi-kafka-operator kafka strimzi-kafka "--set watchAnyNamespace=true"
  phase6_install_one minio https://operator.min.io minio/operator minio-operator minio-operator
  log "INFO" "Operators installed"
}

phase7_platform() {
  log "STEP" "Phase 7: Platform manifests (domain=$DOMAIN)"
  kubectl apply -f "$REPO_ROOT/deploy/k8s/platform/namespaces.yaml"
  run_cmd_visible bash -c "kubectl kustomize \"$REPO_ROOT/deploy/k8s/platform\" | sed \"s/urfu-link\.local/$DOMAIN/g\" | kubectl apply -f -"
  sleep 5
  local missing=""
  kubectl get ingress api-gateway -n urfu-prod &>/dev/null || missing="api-gateway(urfu-prod)"
  kubectl get ingress frontend-web -n urfu-prod &>/dev/null || missing="${missing:+$missing }frontend-web(urfu-prod)"
  kubectl get ingress keycloak -n urfu-platform &>/dev/null || missing="${missing:+$missing }keycloak(urfu-platform)"
  if [[ -n "$missing" ]]; then
    log "ERROR" "Platform apply: missing Ingress: $missing"
    exit 1
  fi
  log "INFO" "Platform Ingress verified (api-gateway, frontend-web, keycloak)"
  if [[ "$SKIP_VAULT" == "true" ]]; then
    log "INFO" "Vault skipped: create secrets manually for Keycloak and services in urfu-prod"
  fi
}

phase7b_headlamp() {
  log "STEP" "Phase 7b: Headlamp dashboard (OIDC via Keycloak)"
  if [[ "$SKIP_VAULT" != "true" ]]; then
    log "INFO" "Waiting for headlamp-oidc secret (ESO sync)..."
    for i in {1..24}; do
      if kubectl get secret headlamp-oidc -n urfu-platform &>/dev/null; then
        break
      fi
      sleep 5
    done
  fi
  helm repo add headlamp https://kubernetes-sigs.github.io/headlamp/ 2>/dev/null || true
  helm repo update headlamp
  kubectl create namespace urfu-platform --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install headlamp headlamp/headlamp -n urfu-platform -f "$REPO_ROOT/deploy/helm/services/headlamp/values-prod.yaml" \
    --set ingress.hosts[0].host=k8s.$DOMAIN \
    --set ingress.tls[0].hosts[0]=k8s.$DOMAIN \
    --set ingress.tls[0].secretName=headlamp-tls \
    --wait --timeout 3m
  local headlamp_host
  headlamp_host=$(kubectl get ingress -n urfu-platform -o jsonpath='{.items[*].spec.rules[0].host}' 2>/dev/null | tr ' ' '\n' | grep "k8s\.$DOMAIN" || true)
  if [[ -z "$headlamp_host" ]]; then
    log "ERROR" "Headlamp Ingress (k8s.$DOMAIN) not found in urfu-platform"
    exit 1
  fi
  log "INFO" "Headlamp: https://k8s.$DOMAIN (OIDC callback: https://k8s.$DOMAIN/oidc-callback)"
}

phase7c_wait_certs() {
  log "STEP" "Phase 7c: Wait for TLS certificates (cert-manager)"
  local max_wait=300
  local elapsed=0
  while [[ $elapsed -lt $max_wait ]]; do
    local api_ready app_ready
    api_ready=$(kubectl get certificate api-ghjc-ru-tls -n urfu-prod -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || echo "")
    app_ready=$(kubectl get certificate app-ghjc-ru-tls -n urfu-prod -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || echo "")
    if [[ "$api_ready" == "True" ]] && [[ "$app_ready" == "True" ]]; then
      log "INFO" "TLS certificates ready (api.${DOMAIN}, app.${DOMAIN})"
      return 0
    fi
    sleep 10
    elapsed=$((elapsed + 10))
  done
  log "INFO" "TLS wait timeout (${max_wait}s). Check: kubectl describe certificate -n urfu-prod; kubectl get challenge -A"
}

phase8_stateful() {
  log "STEP" "Phase 8: Stateful stack (120GB storage profile)"
  kubectl kustomize "$REPO_ROOT/deploy/k8s/platform/stateful" | sed -e "$STATEFUL_STORAGE_SED" | kubectl apply -f -

  log "INFO" "Waiting for PostgreSQL cluster..."
  for i in {1..60}; do
    if kubectl get cluster -n urfu-platform urfu-postgres -o jsonpath='{.status.phase}' 2>/dev/null | grep -qE 'Cluster in healthy state|running'; then
      break
    fi
    [[ $i -eq 60 ]] && log "INFO" "PostgreSQL wait timeout"
    sleep 10
  done

  log "INFO" "Waiting for Kafka..."
  for i in {1..60}; do
    if kubectl get kafka -n urfu-platform urfu-kafka -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null | grep -q True; then
      break
    fi
    [[ $i -eq 60 ]] && log "INFO" "Kafka wait timeout"
    sleep 10
  done
}

phase9_services() {
  log "STEP" "Phase 9: Deploy services (urfu-prod)"
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  if ! kubectl cluster-info &>/dev/null; then
    log "ERROR" "Cluster unreachable (KUBECONFIG=$KUBECONFIG). Deploy services manually: export KUBECONFIG=$KUBECONFIG"
    exit 1
  fi
  local services=(api-gateway media-service user-service chat-service presence-service notification-service call-service frontend-web)
  for svc in "${services[@]}"; do
    log "INFO" "Deploying $svc..."
    helm upgrade --install "$svc" "$REPO_ROOT/deploy/helm/charts/urfu-service" -n urfu-prod --create-namespace -f "$REPO_ROOT/deploy/helm/services/$svc/values-prod.yaml"
  done
  log "INFO" "Services deployed"
}

phase10_wait_smoke() {
  log "STEP" "Phase 10: Wait for pods and smoke"
  log "INFO" "Waiting for urfu-prod pods..."
  for i in {1..60}; do
    local ready total
    ready=$(kubectl get pods -n urfu-prod -l 'app.kubernetes.io/name' -o jsonpath='{.items[*].status.conditions[?(@.type=="Ready")].status}' 2>/dev/null | tr ' ' '\n' | grep -c True || echo 0)
    total=$(kubectl get pods -n urfu-prod -l 'app.kubernetes.io/name' --no-headers 2>/dev/null | wc -l)
    [[ "$total" -gt 0 ]] && [[ "$ready" -ge "$((total/2))" ]] && break
    [[ $i -eq 60 ]] && log "INFO" "Pods wait timeout"
    sleep 5
  done
  log "INFO" "Bootstrap complete. API: https://api.$DOMAIN Frontend: https://app.$DOMAIN Headlamp: https://k8s.$DOMAIN"
}

main() {
  init_log
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  log "INFO" "Start DOMAIN=$DOMAIN REPO_ROOT=$REPO_ROOT KUBECONFIG=$KUBECONFIG"
  phase0_env
  phase1_host_and_k3s
  phase2_ingress_certmanager
  phase3_linkerd
  phase4_argocd
  phase5_eso
  phase6_operators
  phase7_platform
  phase7b_headlamp
  phase7c_wait_certs
  phase8_stateful
  phase9_services
  phase10_wait_smoke
  log "INFO" "End OK"
}

main "$@"
