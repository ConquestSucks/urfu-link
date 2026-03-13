#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${DOMAIN:-ghjc.ru}"
SOURCE_DOMAIN="${SOURCE_DOMAIN:-urfu-link.local}"
REPO_ROOT="${REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
LOG_FILE="${LOG_FILE:-}"
SKIP_VAULT="${SKIP_VAULT:-false}"
INSTALL_LINKERD="${INSTALL_LINKERD:-true}"
LINKERD2_VERSION="${LINKERD2_VERSION:-edge-25.10.7}"
GATEWAY_API_VERSION="${GATEWAY_API_VERSION:-v1.4.0}"
ARGOCD_VERSION="${ARGOCD_VERSION:-v2.14.11}"
ARGO_ROLLOUTS_VERSION="${ARGO_ROLLOUTS_VERSION:-v1.8.3}"
APT_UPGRADE="${APT_UPGRADE:-false}"
NET_RETRIES="${NET_RETRIES:-5}"
NET_RETRY_DELAY_SEC="${NET_RETRY_DELAY_SEC:-10}"
NET_TIMEOUT_SEC="${NET_TIMEOUT_SEC:-60}"
K8S_WAIT_TIMEOUT="${K8S_WAIT_TIMEOUT:-300s}"
POD_POLL_INTERVAL_SEC="${POD_POLL_INTERVAL_SEC:-5}"
STATEFUL_POLL_INTERVAL_SEC="${STATEFUL_POLL_INTERVAL_SEC:-10}"
OPERATOR_PARALLELISM="${OPERATOR_PARALLELISM:-2}"
SERVICE_PARALLELISM="${SERVICE_PARALLELISM:-3}"
DISK_MIN_GB=90
RAM_MIN_MB=3500
STATEFUL_OVERLAY="${STATEFUL_OVERLAY:-$REPO_ROOT/deploy/k8s/platform/stateful-prod}"
declare -A HELM_REPOS_UPDATED=()
declare -A PHASE_DURATIONS=()
SERVICES=(api-gateway media-service user-service chat-service presence-service notification-service call-service frontend-web)

PROXY_SCHEME="${PROXY_SCHEME:-socks5}"
PROXY_HOST="${PROXY_HOST:-}"
PROXY_PORT="${PROXY_PORT:-1080}"
PROXY_USERNAME="${PROXY_USERNAME:-}"
PROXY_PASSWORD="${PROXY_PASSWORD:-}"
PROXY_AUTH=""
if [[ -n "$PROXY_USERNAME" ]] && [[ -n "$PROXY_PASSWORD" ]]; then
  PROXY_AUTH="${PROXY_USERNAME}:${PROXY_PASSWORD}@"
fi
PROXY_URL="${PROXY_URL:-}"
if [[ -z "$PROXY_URL" ]] && [[ -n "$PROXY_HOST" ]]; then
  PROXY_URL="${PROXY_SCHEME}://${PROXY_AUTH}${PROXY_HOST}:${PROXY_PORT}"
fi
if [[ -n "$PROXY_URL" ]]; then
  export HTTP_PROXY="$PROXY_URL"
  export HTTPS_PROXY="$PROXY_URL"
  export ALL_PROXY="$PROXY_URL"
fi
export NO_PROXY="${NO_PROXY:-localhost,127.0.0.1,10.0.0.0/8,192.168.0.0/16,172.16.0.0/12,.svc,.cluster.local,.urfu-link.local,.ghjc.ru}"

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

configure_apt_proxy() {
  local run=""
  [[ "$(id -u)" -ne 0 ]] && run="sudo"
  local apt_proxy_file="/etc/apt/apt.conf.d/99proxy"
  local proxy_url="${HTTP_PROXY:-${ALL_PROXY:-}}"

  if [[ -z "$proxy_url" ]]; then
    if [[ -f "$apt_proxy_file" ]]; then
      $run rm -f "$apt_proxy_file"
      log "INFO" "Removed apt proxy config: $apt_proxy_file"
    fi
    return 0
  fi

  if [[ "$proxy_url" =~ ^https?:// ]]; then
    {
      echo "Acquire::http::Proxy \"$proxy_url\";"
      echo "Acquire::https::Proxy \"$proxy_url\";"
    } | $run tee "$apt_proxy_file" >/dev/null
    log "INFO" "Configured apt proxy: $proxy_url"
    return 0
  fi

  if [[ -f "$apt_proxy_file" ]]; then
    $run rm -f "$apt_proxy_file"
    log "INFO" "Removed unsupported apt proxy config for scheme in $proxy_url"
  fi
  log "INFO" "Skipping apt proxy configuration for unsupported scheme: $proxy_url"
}

timestamp_now() {
  date +%s
}

record_phase_duration() {
  local phase_name="$1" started_at="$2"
  local duration=$(( $(timestamp_now) - started_at ))
  PHASE_DURATIONS["$phase_name"]="$duration"
  log "INFO" "$phase_name completed in ${duration}s"
}

print_phase_summary() {
  local phase_name
  log "STEP" "Phase duration summary"
  for phase_name in \
    phase0_env phase1_host_and_k3s phase2_ingress_certmanager phase3_linkerd phase4_argocd \
    phase4b_vault phase5_eso phase6_operators phase6b_observability phase7_platform phase7b_headlamp \
    phase8_stateful phase9_services phase10_wait_smoke; do
    if [[ -n "${PHASE_DURATIONS[$phase_name]+x}" ]]; then
      log "INFO" "$phase_name=${PHASE_DURATIONS[$phase_name]}s"
    fi
  done
}

run_cmd() {
  log "CMD" "$*"
  "$@" >> "$LOG_FILE" 2>&1
}

run_cmd_visible() {
  log "CMD" "$*"
  "$@" 2>&1 | tee -a "$LOG_FILE"
}

normalize_ubuntu_apt_sources() {
  local run=""
  [[ "$(id -u)" -ne 0 ]] && run="sudo"
  if [[ ! -r /etc/os-release ]]; then
    return 0
  fi
  . /etc/os-release
  if [[ "${ID:-}" != "ubuntu" ]]; then
    return 0
  fi

  local apt_arch="${DPKG_ARCHITECTURE:-}"
  if [[ -z "$apt_arch" ]] && command -v dpkg &>/dev/null; then
    apt_arch=$(dpkg --print-architecture 2>/dev/null || true)
  fi
  if [[ -z "$apt_arch" ]]; then
    apt_arch=$(uname -m 2>/dev/null || true)
  fi

  local changed=false
  local sources_files=()
  [[ -f /etc/apt/sources.list ]] && sources_files+=(/etc/apt/sources.list)
  while IFS= read -r file; do
    sources_files+=("$file")
  done < <(find /etc/apt/sources.list.d -maxdepth 1 \( -name '*.list' -o -name '*.sources' \) 2>/dev/null | sort)

  local file
  for file in "${sources_files[@]}"; do
    if grep -Eq 'ru\.archive\.ubuntu\.com|archive\.ubuntu\.com|security\.ubuntu\.com|ports\.ubuntu\.com/ubuntu-ports|noble-proposed|Suites:\s+.*-proposed' "$file" 2>/dev/null; then
      local tmp
      tmp=$(mktemp)
      python3 - "$file" "$VERSION_CODENAME" "$apt_arch" >"$tmp" <<'PY'
from pathlib import Path
import re
import sys

path = Path(sys.argv[1])
codename = sys.argv[2]
apt_arch = sys.argv[3]
text = path.read_text()
primary_arches = {"amd64", "i386"}
ports_arches = {"arm64", "armhf", "ppc64el", "s390x", "riscv64", "aarch64", "armv7l", "armv8l"}
use_ports = apt_arch in ports_arches or apt_arch not in primary_arches

if use_ports:
    replacements = {
        "http://ru.archive.ubuntu.com/ubuntu": "https://ports.ubuntu.com/ubuntu-ports",
        "https://ru.archive.ubuntu.com/ubuntu": "https://ports.ubuntu.com/ubuntu-ports",
        "http://archive.ubuntu.com/ubuntu": "https://ports.ubuntu.com/ubuntu-ports",
        "https://archive.ubuntu.com/ubuntu": "https://ports.ubuntu.com/ubuntu-ports",
        "http://security.ubuntu.com/ubuntu": "https://ports.ubuntu.com/ubuntu-ports",
        "https://security.ubuntu.com/ubuntu": "https://ports.ubuntu.com/ubuntu-ports",
    }
else:
    replacements = {
        "http://ru.archive.ubuntu.com/ubuntu": "https://archive.ubuntu.com/ubuntu",
        "https://ru.archive.ubuntu.com/ubuntu": "https://archive.ubuntu.com/ubuntu",
        "http://archive.ubuntu.com/ubuntu": "https://archive.ubuntu.com/ubuntu",
        "http://security.ubuntu.com/ubuntu": "https://security.ubuntu.com/ubuntu",
    }

for old, new in replacements.items():
    text = text.replace(old, new)

text = re.sub(rf"^([ \t]*deb(?:-src)?\s+\S+\s+{re.escape(codename)}-proposed\b.*)$", r"# \1", text, flags=re.MULTILINE)
text = re.sub(rf"^(Suites:\s.*)\b{re.escape(codename)}-proposed\b(.*)$", lambda m: m.group(1) + m.group(2), text, flags=re.MULTILINE)
text = re.sub(r"^(Suites:\s+)\s+", r"\1", text, flags=re.MULTILINE)
print(text, end="" if text.endswith("\n") else "\n")
PY
      if ! cmp -s "$file" "$tmp"; then
        cat "$tmp" | $run tee "$file" >/dev/null
        changed=true
        log "INFO" "Normalized Ubuntu apt source file: $file"
      fi
      rm -f "$tmp"
    fi
  done

  if [[ "$changed" == true ]]; then
    log "INFO" "Ubuntu apt sources normalized"
  fi
}

retry_cmd() {
  local attempt=1 max="${1:-$NET_RETRIES}" delay="${2:-$NET_RETRY_DELAY_SEC}"
  shift 2
  while true; do
    if "$@"; then
      return 0
    fi
    if [[ $attempt -ge $max ]]; then
      log "ERROR" "Command failed after $max attempts: $*"
      return 1
    fi
    log "INFO" "Command failed (attempt $attempt/$max), retry in ${delay}s: $*"
    sleep "$delay"
    attempt=$((attempt + 1))
  done
}

download_to_file() {
  local url="$1" target="$2"
  retry_cmd "$NET_RETRIES" "$NET_RETRY_DELAY_SEC" \
    curl --fail --show-error --silent --location --connect-timeout "$NET_TIMEOUT_SEC" --max-time "$NET_TIMEOUT_SEC" \
    -o "$target" "$url"
}

kubectl_apply_url() {
  local url="$1"
  local manifest
  manifest=$(mktemp)
  download_to_file "$url" "$manifest"
  kubectl apply --server-side -f "$manifest"
  rm -f "$manifest"
}

helm_repo_add_update() {
  local name="$1" repo_url="$2"
  log "CMD" "helm repo add --force-update $name $repo_url"
  retry_cmd "$NET_RETRIES" "$NET_RETRY_DELAY_SEC" \
    helm repo add --force-update "$name" "$repo_url" >> "$LOG_FILE" 2>&1
  if [[ -z "${HELM_REPOS_UPDATED[$name]+x}" ]]; then
    log "CMD" "helm repo update $name"
    retry_cmd "$NET_RETRIES" "$NET_RETRY_DELAY_SEC" \
      helm repo update "$name" >> "$LOG_FILE" 2>&1
    HELM_REPOS_UPDATED[$name]=1
  else
    log "INFO" "Helm repo $name already refreshed in this run; skipping update"
  fi
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
  if ! command -v kubectl &>/dev/null && command -v k3s &>/dev/null; then
    export PATH="$PATH:/usr/local/bin"
  fi
  if ! command -v mktemp &>/dev/null; then
    log "ERROR" "mktemp required"
    exit 1
  fi
  local avail_gb
  avail_gb=$(df -BG "$REPO_ROOT" 2>/dev/null | awk 'NR==2 {gsub(/G/,""); print $4}')
  if [[ -n "$avail_gb" ]] && [[ "$avail_gb" -lt "$DISK_MIN_GB" ]]; then
    log "ERROR" "Low disk: ${avail_gb}GB (min ${DISK_MIN_GB}GB)"
    exit 1
  fi
  log "INFO" "Domain=$DOMAIN SourceDomain=$SOURCE_DOMAIN SkipVault=$SKIP_VAULT InstallLinkerd=$INSTALL_LINKERD AptUpgrade=$APT_UPGRADE"
}

phase1_host_and_k3s() {
  log "STEP" "Phase 1: Host preparation and k3s"
  local run=""
  [[ "$(id -u)" -ne 0 ]] && run="sudo"

  configure_apt_proxy
  normalize_ubuntu_apt_sources
  log "INFO" "apt update"
  $run apt-get update -qq
  if [[ "$APT_UPGRADE" == "true" ]]; then
    log "INFO" "apt upgrade"
    env DEBIAN_FRONTEND=noninteractive $run apt-get upgrade -y -qq
  else
    log "INFO" "Skipping apt upgrade (APT_UPGRADE != true)"
  fi
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
    local k3s_install
    k3s_install=$(mktemp)
    download_to_file https://get.k3s.io "$k3s_install"
    run_cmd_visible $run sh "$k3s_install" -s - --disable traefik
    rm -f "$k3s_install"
    if [[ "$(id -u)" -ne 0 ]]; then
      $run chmod 600 /etc/rancher/k3s/k3s.yaml
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
    local helm_install
    helm_install=$(mktemp)
    download_to_file https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 "$helm_install"
    run_cmd_visible bash "$helm_install"
    rm -f "$helm_install"
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
    helm_repo_add_update ingress-nginx https://kubernetes.github.io/ingress-nginx
    helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n ingress-nginx --wait --timeout 5m
  fi

  kubectl create namespace cert-manager --dry-run=client -o yaml | kubectl apply -f -
  if ! helm status cert-manager -n cert-manager &>/dev/null; then
    helm_repo_add_update jetstack https://charts.jetstack.io
    helm upgrade --install cert-manager jetstack/cert-manager -n cert-manager \
      --set installCRDs=true --wait --timeout 5m
  fi

  sed "s/${SOURCE_DOMAIN//./\\.}/$DOMAIN/g" "$REPO_ROOT/deploy/k8s/platform/cert-manager/cluster-issuer.yaml" | kubectl apply -f -
}

phase3_linkerd() {
  if [[ "$INSTALL_LINKERD" != "true" ]]; then
    log "INFO" "Phase 3: Linkerd skipped (INSTALL_LINKERD != true)"
    return 0
  fi
  log "STEP" "Phase 3: Linkerd + Viz"
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  if ! command -v linkerd &>/dev/null; then
    local linkerd_install
    linkerd_install=$(mktemp)
    download_to_file https://run.linkerd.io/install-edge "$linkerd_install"
    run_cmd_visible env LINKERD2_VERSION="$LINKERD2_VERSION" sh "$linkerd_install"
    rm -f "$linkerd_install"
    export PATH="$PATH:$HOME/.linkerd2/bin"
  fi
  kubectl_apply_url "https://github.com/kubernetes-sigs/gateway-api/releases/download/${GATEWAY_API_VERSION}/standard-install.yaml" 2>/dev/null || true
  crds_out=""
  crds_out=$(linkerd upgrade --crds 2>/dev/null) || true
  if [[ -n "${crds_out//[[:space:]]/}" ]]; then
    echo "$crds_out" | kubectl apply -f -
    log "INFO" "Linkerd CRDs upgraded"
  else
    linkerd install --crds | kubectl apply -f -
  fi
  upgrade_out=""
  upgrade_out=$(linkerd upgrade 2>/dev/null) || true
  upgrade_manifests=""
  if [[ -n "${upgrade_out//[[:space:]]/}" ]]; then
    upgrade_manifests=$(echo "$upgrade_out" | awk '/^apiVersion:|^---/{f=1}f')
  fi
  if [[ -n "${upgrade_manifests//[[:space:]]/}" ]]; then
    echo "$upgrade_manifests" | kubectl apply -f -
    log "INFO" "Linkerd control plane upgraded"
  else
    log "INFO" "Linkerd upgrade produced no manifests, trying install or continuing if already present"
    linkerd_stderr=$(mktemp)
    linkerd_install_out=$(mktemp)
    set +e
    linkerd install 2>"$linkerd_stderr" | awk '/^apiVersion:|^---/{f=1}f' >"$linkerd_install_out"
    set -e
    if [[ -s "$linkerd_install_out" ]]; then
      set +o pipefail
      kubectl apply -f "$linkerd_install_out" 2>&1 | tee -a "$LOG_FILE"
      apply_ret=${PIPESTATUS[0]}
      set -o pipefail
    else
      apply_ret=0
    fi
    rm -f "$linkerd_install_out"
    if [[ $apply_ret -ne 0 ]]; then
      if grep -q -e "already exists" -e "linkerd upgrade" "$linkerd_stderr" 2>/dev/null; then
        log "INFO" "Linkerd control plane already installed (install refused), continuing"
      elif kubectl get configmap linkerd-config -n linkerd --request-timeout=5s &>/dev/null; then
        log "INFO" "Linkerd control plane already present (no manifests from install), continuing"
      else
        cat "$linkerd_stderr" >> "$LOG_FILE"
        log "ERROR" "Linkerd install failed (exit $apply_ret)"
        rm -f "$linkerd_stderr"
        exit 1
      fi
    fi
    rm -f "$linkerd_stderr"
  fi
  kubectl wait --namespace linkerd --for=condition=available deployment/linkerd-destination deployment/linkerd-identity deployment/linkerd-proxy-injector --timeout="$K8S_WAIT_TIMEOUT"
  viz_out=""
  viz_out=$(linkerd viz upgrade 2>/dev/null) || true
  viz_manifests=""
  if [[ -n "${viz_out//[[:space:]]/}" ]]; then
    viz_manifests=$(echo "$viz_out" | awk '/^apiVersion:|^---/{f=1}f')
  fi
  if [[ -n "${viz_manifests//[[:space:]]/}" ]]; then
    echo "$viz_manifests" | kubectl apply -f -
    log "INFO" "Linkerd Viz upgraded"
  else
    log "INFO" "Linkerd Viz upgrade produced no manifests, trying install or continuing if already present"
    viz_stderr=$(mktemp)
    viz_install_out=$(mktemp)
    set +e
    linkerd viz install 2>"$viz_stderr" | awk '/^apiVersion:|^---/{f=1}f' >"$viz_install_out"
    viz_install_ret=${PIPESTATUS[0]}
    set -e
    if [[ -s "$viz_install_out" ]]; then
      set +o pipefail
      kubectl apply -f "$viz_install_out" 2>&1 | tee -a "$LOG_FILE"
      viz_apply_ret=${PIPESTATUS[0]}
      set -o pipefail
    else
      viz_apply_ret=0
    fi
    rm -f "$viz_install_out"
    if [[ $viz_apply_ret -ne 0 ]]; then
      if grep -q -e "already exists" -e "linkerd.*upgrade" "$viz_stderr" 2>/dev/null; then
        log "INFO" "Linkerd Viz already installed (install refused), continuing"
      elif KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}" kubectl get deployment metrics-api -n linkerd-viz --request-timeout=5s &>/dev/null; then
        log "INFO" "Linkerd Viz already present (no manifests from install), continuing"
      else
        cat "$viz_stderr" >> "$LOG_FILE"
        log "INFO" "Linkerd Viz install produced no objects (exit $viz_apply_ret), continuing to wait"
      fi
    fi
    rm -f "$viz_stderr"
  fi
  kubectl wait --namespace linkerd-viz --for=condition=available deployment/metrics-api deployment/web --timeout="$K8S_WAIT_TIMEOUT"
}

phase4_argocd() {
  log "STEP" "Phase 4: Argo CD and Argo Rollouts"
  kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply -f -
  if ! kubectl get deployment argocd-server -n argocd &>/dev/null; then
    kubectl_apply_url "https://raw.githubusercontent.com/argoproj/argo-cd/${ARGOCD_VERSION}/manifests/install.yaml"
  fi
  kubectl wait --namespace argocd --for=condition=available deployment/argocd-server deployment/argocd-repo-server --timeout="$K8S_WAIT_TIMEOUT"
  kubectl rollout status statefulset/argocd-application-controller -n argocd --timeout="$K8S_WAIT_TIMEOUT"
  kubectl create namespace argo-rollouts --dry-run=client -o yaml | kubectl apply -f -
  if ! kubectl get deployment argo-rollouts -n argo-rollouts &>/dev/null; then
    kubectl_apply_url "https://github.com/argoproj/argo-rollouts/releases/download/${ARGO_ROLLOUTS_VERSION}/install.yaml"
  fi
  kubectl wait --namespace argo-rollouts --for=condition=available deployment/argo-rollouts --timeout="$K8S_WAIT_TIMEOUT"
}

phase4b_vault() {
  if [[ "$SKIP_VAULT" == "true" ]]; then
    log "INFO" "Phase 4b: Vault skipped (SKIP_VAULT=true)"
    return 0
  fi
  log "STEP" "Phase 4b: HashiCorp Vault (in-cluster)"
  helm_repo_add_update hashicorp https://helm.releases.hashicorp.com
  kubectl create namespace vault --dry-run=client -o yaml | kubectl apply -f -
  
  if ! helm status vault -n vault &>/dev/null; then
    log "INFO" "Installing Vault..."
    helm upgrade --install vault hashicorp/vault -n vault \
      --set "server.standalone.enabled=true" \
      --set "server.standalone.config= |
        ui = true
        listener \"tcp\" {
          tls_disable = 1
          address = \"[::]:8200\"
          cluster_address = \"[::]:8201\"
        }
        storage \"file\" {
          path = \"/vault/data\"
        }" \
      --wait --timeout 5m
  fi
  
  kubectl wait --namespace vault --for=condition=ready pod/vault-0 --timeout="$K8S_WAIT_TIMEOUT"
  
  # Check if Vault is initialized
  local init_status
  init_status=$(kubectl exec -n vault vault-0 -- vault status -format=json 2>/dev/null | grep '"initialized"' | grep -o 'true\|false' || echo "false")
  
  if [[ "$init_status" == "false" ]]; then
    log "INFO" "Initializing Vault..."
    kubectl exec -n vault vault-0 -- vault operator init -key-shares=1 -key-threshold=1 -format=json > "$REPO_ROOT/vault-keys.json"
    local unseal_key
    unseal_key=$(grep -o '"unseal_keys_b64": \[[^]]*\]' "$REPO_ROOT/vault-keys.json" | grep -o '"[^"]*"' | head -1 | tr -d '"')
    local root_token
    root_token=$(grep -o '"root_token": "[^"]*"' "$REPO_ROOT/vault-keys.json" | grep -o '"[^"]*"' | tail -1 | tr -d '"')
    
    log "INFO" "Unsealing Vault..."
    kubectl exec -n vault vault-0 -- vault operator unseal "$unseal_key"
    
    log "INFO" "Configuring Vault Kubernetes Auth..."
    kubectl exec -n vault vault-0 -- sh -c "
      export VAULT_TOKEN=$root_token
      vault auth enable kubernetes
      vault write auth/kubernetes/config \
        kubernetes_host=https://\$KUBERNETES_PORT_443_TCP_ADDR:443
      
      vault policy write urfu-link-prod - <<EOF
path \"kv/*\" {
  capabilities = [\"read\", \"list\"]
}
EOF
      
      vault write auth/kubernetes/role/urfu-link-prod \
        bound_service_account_names=external-secrets \
        bound_service_account_namespaces=external-secrets \
        policies=urfu-link-prod \
        ttl=24h
        
      vault secrets enable -version=2 kv
    "
    log "INFO" "Vault initialized. Keys saved to $REPO_ROOT/vault-keys.json (KEEP THIS SAFE!)"
  else
    log "INFO" "Vault already initialized. Checking sealed status..."
    local sealed_status
    sealed_status=$(kubectl exec -n vault vault-0 -- vault status -format=json 2>/dev/null | grep '"sealed"' | grep -o 'true\|false' || echo "true")
    if [[ "$sealed_status" == "true" ]]; then
      log "WARNING" "Vault is sealed! You must unseal it manually."
    else
      log "INFO" "Vault is already unsealed."
    fi
  fi
}

phase5_eso() {
  log "STEP" "Phase 5: External Secrets Operator"
  helm_repo_add_update external-secrets https://charts.external-secrets.io
  kubectl create namespace external-secrets --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install external-secrets external-secrets/external-secrets -n external-secrets --wait --timeout 5m
  kubectl wait --namespace external-secrets --for=condition=available deployment/external-secrets deployment/external-secrets-webhook deployment/external-secrets-cert-controller --timeout="$K8S_WAIT_TIMEOUT"
  [[ "$SKIP_VAULT" == "true" ]] && log "INFO" "SKIP_VAULT=true: configure Vault and secrets later; see README"
  log "INFO" "Phase 5 done"
}

phase6_install_one() {
  local name="$1" repo_url="$2" chart="$3" ns="$4" release_name="${5:-$1}" helm_extra="${6:-}" helm_ret
  local -a helm_extra_args=()
  if [[ -n "$helm_extra" ]]; then
    read -r -a helm_extra_args <<< "$helm_extra"
  fi
  kubectl create namespace "$ns" --dry-run=client -o yaml | kubectl apply -f -
  
  if helm status "$release_name" -n "$ns" &>/dev/null; then
    log "INFO" "Release $release_name is already installed in $ns, skipping..."
    return 0
  fi
  
  log "INFO" "Installing $release_name in $ns..."
  set +o pipefail
  helm upgrade --install "$release_name" "$chart" -n "$ns" "${helm_extra_args[@]}" --wait --timeout 5m 2>&1 | tee -a "$LOG_FILE"
  helm_ret=${PIPESTATUS[0]}
  set -o pipefail
  if [[ $helm_ret -ne 0 ]]; then
    log "ERROR" "helm upgrade --install $release_name failed (exit $helm_ret)"
    exit 1
  fi
}

wait_for_background_jobs() {
  local max_parallel="$1"
  shift
  local -n pid_list_ref=$1
  local -n name_map_ref=$2
  local finished_pid
  local active_count
  while true; do
    active_count=0
    for finished_pid in "${pid_list_ref[@]}"; do
      if kill -0 "$finished_pid" 2>/dev/null; then
        active_count=$((active_count + 1))
      fi
    done
    if [[ $active_count -lt $max_parallel ]]; then
      return 0
    fi
    sleep 1
  done
}

collect_background_jobs() {
  local -n pid_list_ref=$1
  local -n name_map_ref=$2
  local pid
  local failed=0
  for pid in "${pid_list_ref[@]}"; do
    if wait "$pid"; then
      log "INFO" "${name_map_ref[$pid]} completed"
    else
      log "ERROR" "${name_map_ref[$pid]} failed"
      failed=1
    fi
  done
  return "$failed"
}

phase6_operators() {
  log "STEP" "Phase 6: Stateful operators"
  
  # Pre-flight: add all repos sequentially to avoid lock issues
  log "INFO" "Adding Helm repositories..."
  helm_repo_add_update cnpg https://cloudnative-pg.github.io/charts
  helm_repo_add_update mongodb https://mongodb.github.io/helm-charts
  helm_repo_add_update ot-helm https://ot-container-kit.github.io/helm-charts
  helm_repo_add_update strimzi https://strimzi.io/charts
  helm_repo_add_update minio https://operator.min.io
  
  local -a pids=()
  declare -A pid_to_name=()

  phase6_install_one cnpg https://cloudnative-pg.github.io/charts cnpg/cloudnative-pg cnpg-system &
  pids+=("$!")
  pid_to_name[$!]="cnpg"
  wait_for_background_jobs "$OPERATOR_PARALLELISM" pids pid_to_name

  phase6_install_one mongodb https://mongodb.github.io/helm-charts mongodb/community-operator mongodb community-operator &
  pids+=("$!")
  pid_to_name[$!]="mongodb"
  wait_for_background_jobs "$OPERATOR_PARALLELISM" pids pid_to_name

  phase6_install_one ot-helm https://ot-container-kit.github.io/helm-charts ot-helm/redis-operator redis-operator redis-operator &
  pids+=("$!")
  pid_to_name[$!]="redis-operator"
  wait_for_background_jobs "$OPERATOR_PARALLELISM" pids pid_to_name

  phase6_install_one strimzi https://strimzi.io/charts strimzi/strimzi-kafka-operator kafka strimzi-kafka "--set watchAnyNamespace=true" &
  pids+=("$!")
  pid_to_name[$!]="strimzi"
  wait_for_background_jobs "$OPERATOR_PARALLELISM" pids pid_to_name

  phase6_install_one minio https://operator.min.io minio/operator minio-operator minio-operator &
  pids+=("$!")
  pid_to_name[$!]="minio"

  collect_background_jobs pids pid_to_name || exit 1
  log "INFO" "Operators installed"
}

phase6b_observability() {
  log "STEP" "Phase 6b: Observability Stack (Prometheus, Grafana, Loki)"
  helm_repo_add_update prometheus-community https://prometheus-community.github.io/helm-charts
  helm_repo_add_update grafana https://grafana.github.io/helm-charts
  
  kubectl create namespace observability --dry-run=client -o yaml | kubectl apply -f -
  
  log "INFO" "Installing kube-prometheus-stack..."
  if ! helm status kube-prometheus-stack -n observability &>/dev/null; then
    helm upgrade --install kube-prometheus-stack prometheus-community/kube-prometheus-stack -n observability \
      --set grafana.ingress.enabled=true \
      --set grafana.ingress.ingressClassName=nginx \
      --set grafana.ingress.hosts[0]=grafana.$DOMAIN \
      --set grafana.ingress.tls[0].hosts[0]=grafana.$DOMAIN \
      --set grafana.ingress.tls[0].secretName=grafana-tls \
      --set grafana.ingress.annotations."cert-manager\.io/cluster-issuer"=letsencrypt-production \
      --set grafana.adminPassword=admin \
      --set prometheus.prometheusSpec.remoteWriteReceiver.enabled=true \
      --wait --timeout 10m
  fi
  
  log "INFO" "Installing loki-stack..."
  if ! helm status loki -n observability &>/dev/null; then
    helm upgrade --install loki grafana/loki-stack -n observability \
      --set promtail.enabled=true \
      --wait --timeout 10m
  fi
  
  log "INFO" "Observability stack installed. Grafana: https://grafana.$DOMAIN"
}

phase7_platform() {
  log "STEP" "Phase 7: Platform manifests (domain=$DOMAIN)"
  kubectl apply -f "$REPO_ROOT/deploy/k8s/platform/namespaces.yaml"
  run_cmd_visible bash -c "kubectl kustomize \"$REPO_ROOT/deploy/k8s/platform\" | sed \"s/${SOURCE_DOMAIN//./\\.}/$DOMAIN/g\" | kubectl apply -f -"
  local missing=""
  kubectl get ingress api-gateway -n urfu-prod &>/dev/null || missing="api-gateway(urfu-prod)"
  kubectl get ingress frontend-web -n urfu-prod &>/dev/null || missing="${missing:+$missing }frontend-web(urfu-prod)"
  kubectl get ingress keycloak -n urfu-platform &>/dev/null || missing="${missing:+$missing }keycloak(urfu-platform)"
  if [[ -n "$missing" ]]; then
    log "ERROR" "Platform apply: missing Ingress: $missing"
    exit 1
  fi
  if ! kubectl get deployment keycloak -n urfu-platform &>/dev/null; then
    log "ERROR" "Keycloak deployment not found in urfu-platform"
    exit 1
  fi
  if ! kubectl rollout status deployment/keycloak -n urfu-platform --timeout="$K8S_WAIT_TIMEOUT"; then
    log "INFO" "Keycloak rollout is not ready yet; continuing because first start may wait for secrets, database, or migrations"
  fi
  log "INFO" "Platform Ingress verified (api-gateway, frontend-web, keycloak)"
  if [[ "$SKIP_VAULT" == "true" ]]; then
    log "INFO" "Vault skipped: create secrets manually for Keycloak and services in urfu-prod"
  fi
}

phase7b_headlamp() {
  log "STEP" "Phase 7b: Headlamp dashboard (OIDC via Keycloak)"
  helm_repo_add_update headlamp https://kubernetes-sigs.github.io/headlamp/
  kubectl create namespace urfu-platform --dry-run=client -o yaml | kubectl apply -f -
  helm upgrade --install headlamp headlamp/headlamp -n urfu-platform -f "$REPO_ROOT/deploy/helm/services/headlamp/values-prod.yaml" \
    --set ingress.hosts[0].host=k8s.$DOMAIN \
    --set ingress.tls[0].hosts[0]=k8s.$DOMAIN \
    --set ingress.tls[0].secretName=headlamp-tls \
    --timeout 10m
  if ! kubectl get deployment headlamp -n urfu-platform &>/dev/null; then
    log "ERROR" "Headlamp deployment not found in urfu-platform"
    exit 1
  fi
  if ! kubectl rollout status deployment/headlamp -n urfu-platform --timeout="$K8S_WAIT_TIMEOUT"; then
    log "INFO" "Headlamp rollout is not ready yet; continuing because first start may wait for OIDC configuration, secrets, or ingress/certificate provisioning"
  fi
  local headlamp_host
  headlamp_host=$(kubectl get ingress -n urfu-platform -o jsonpath='{.items[*].spec.rules[0].host}' 2>/dev/null | tr ' ' '\n' | grep "k8s\.$DOMAIN" || true)
  if [[ -z "$headlamp_host" ]]; then
    log "ERROR" "Headlamp Ingress (k8s.$DOMAIN) not found in urfu-platform"
    exit 1
  fi
  log "INFO" "Headlamp: https://k8s.$DOMAIN (OIDC callback: https://k8s.$DOMAIN/oidc-callback)"
}

phase8_stateful() {
  log "STEP" "Phase 8: Stateful stack (overlay: $STATEFUL_OVERLAY)"
  kubectl kustomize "$STATEFUL_OVERLAY" | kubectl apply -f -

  log "INFO" "Waiting for PostgreSQL and Kafka clusters..."
  local pg_ready=false
  local kafka_ready=false
  
  for i in {1..60}; do
    if [[ "$pg_ready" == "false" ]]; then
      if kubectl get cluster -n urfu-platform urfu-postgres -o jsonpath='{.status.phase}' 2>/dev/null | grep -qE 'Cluster in healthy state|running'; then
        log "INFO" "PostgreSQL cluster is ready"
        pg_ready=true
      fi
    fi
    
    if [[ "$kafka_ready" == "false" ]]; then
      if kubectl get kafka -n urfu-platform urfu-kafka -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null | grep -q True; then
        log "INFO" "Kafka cluster is ready"
        kafka_ready=true
      fi
    fi
    
    if [[ "$pg_ready" == "true" ]] && [[ "$kafka_ready" == "true" ]]; then
      log "INFO" "All stateful clusters are ready"
      break
    fi
    
    if [[ $i -eq 60 ]]; then
      log "ERROR" "Stateful wait timeout. pg_ready=$pg_ready, kafka_ready=$kafka_ready"
      exit 1
    fi
    sleep "$STATEFUL_POLL_INTERVAL_SEC"
  done
}

phase9_services() {
  log "STEP" "Phase 9: Deploy services (GitOps via ArgoCD)"
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  if ! kubectl cluster-info &>/dev/null; then
    log "ERROR" "Cluster unreachable (KUBECONFIG=$KUBECONFIG). Deploy services manually: export KUBECONFIG=$KUBECONFIG"
    exit 1
  fi
  
  log "INFO" "Applying ArgoCD ApplicationSet for services..."
  kubectl apply -f "$REPO_ROOT/deploy/k8s/platform/argocd/applicationset.yaml"
  
  log "INFO" "Waiting for ArgoCD applications to be created and synced..."
  sleep 10
  
  local all_synced=false
  for i in {1..60}; do
    local pending_apps=0
    local svc
    
    for svc in "${SERVICES[@]}"; do
      local app_name="${svc}-prod"
      local sync_status health_status
      sync_status=$(kubectl get application "$app_name" -n argocd -o jsonpath='{.status.sync.status}' 2>/dev/null || echo "Unknown")
      health_status=$(kubectl get application "$app_name" -n argocd -o jsonpath='{.status.health.status}' 2>/dev/null || echo "Unknown")
      
      if [[ "$sync_status" != "Synced" ]] || [[ "$health_status" != "Healthy" ]]; then
        pending_apps=$((pending_apps + 1))
      fi
    done
    
    if [[ $pending_apps -eq 0 ]]; then
      log "INFO" "All services are Synced and Healthy via ArgoCD"
      all_synced=true
      break
    fi
    
    if [[ $i -eq 60 ]]; then
      log "ERROR" "Timeout waiting for $pending_apps Applications to sync"
      exit 1
    fi
    
    log "INFO" "Waiting for $pending_apps applications to sync... (attempt $i/60)"
    sleep 10
  done
}

phase10_wait_smoke() {
  log "STEP" "Phase 10: Wait for pods and smoke"
  log "INFO" "Waiting for urfu-prod deployments..."
  local svc
  for svc in "${SERVICES[@]}"; do
    if ! kubectl get deployment "$svc" -n urfu-prod &>/dev/null; then
      log "ERROR" "Deployment $svc not found in urfu-prod"
      exit 1
    fi
    kubectl rollout status "deployment/$svc" -n urfu-prod --timeout="$K8S_WAIT_TIMEOUT"
  done
  
  log "INFO" "Checking systemic components (Vault, Prometheus, Grafana)..."
  if [[ "$SKIP_VAULT" != "true" ]]; then
    kubectl rollout status statefulset/vault -n vault --timeout="$K8S_WAIT_TIMEOUT" || log "WARNING" "Vault not fully ready"
  fi
  kubectl rollout status deployment/kube-prometheus-stack-grafana -n observability --timeout="$K8S_WAIT_TIMEOUT" || log "WARNING" "Grafana not fully ready"
  kubectl rollout status statefulset/prometheus-kube-prometheus-stack-prometheus -n observability --timeout="$K8S_WAIT_TIMEOUT" || log "WARNING" "Prometheus not fully ready"
  
  log "INFO" "Waiting for all Pods in urfu-prod to be Ready..."
  kubectl wait --namespace urfu-prod --for=condition=Ready pods -l 'app.kubernetes.io/name' --timeout="$K8S_WAIT_TIMEOUT" || true
  
  local ready total
  ready=$(kubectl get pods -n urfu-prod -l 'app.kubernetes.io/name' -o jsonpath='{.items[*].status.conditions[?(@.type=="Ready")].status}' 2>/dev/null | tr ' ' '\n' | grep -c True || echo 0)
  total=$(kubectl get pods -n urfu-prod -l 'app.kubernetes.io/name' --no-headers 2>/dev/null | wc -l)
  if [[ "$total" -eq 0 ]] || [[ "$ready" -ne "$total" ]]; then
    log "ERROR" "Smoke check failed: ready pods $ready/$total in urfu-prod"
    exit 1
  fi
  
  log "INFO" "============================================================"
  log "INFO" "Bootstrap complete."
  log "INFO" "API Gateway: https://api.$DOMAIN"
  log "INFO" "Frontend App: https://app.$DOMAIN"
  log "INFO" "ArgoCD UI: (Port-forward needed, or Ingress to be configured manually)"
  log "INFO" "Kubernetes Headlamp: https://k8s.$DOMAIN"
  log "INFO" "Grafana: https://grafana.$DOMAIN (admin / admin)"
  if [[ "$SKIP_VAULT" != "true" ]]; then
    log "INFO" "HashiCorp Vault: https://vault.$DOMAIN"
  fi
  log "INFO" "============================================================"
}

main() {
  local phase_started_at
  init_log
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  log "INFO" "Start DOMAIN=$DOMAIN SOURCE_DOMAIN=$SOURCE_DOMAIN REPO_ROOT=$REPO_ROOT STATEFUL_OVERLAY=$STATEFUL_OVERLAY KUBECONFIG=$KUBECONFIG OPERATOR_PARALLELISM=$OPERATOR_PARALLELISM SERVICE_PARALLELISM=$SERVICE_PARALLELISM"

  phase_started_at=$(timestamp_now); phase0_env; record_phase_duration phase0_env "$phase_started_at"
  phase_started_at=$(timestamp_now); phase1_host_and_k3s; record_phase_duration phase1_host_and_k3s "$phase_started_at"
  phase_started_at=$(timestamp_now); phase2_ingress_certmanager; record_phase_duration phase2_ingress_certmanager "$phase_started_at"
  phase_started_at=$(timestamp_now); phase3_linkerd; record_phase_duration phase3_linkerd "$phase_started_at"
  phase_started_at=$(timestamp_now); phase4_argocd; record_phase_duration phase4_argocd "$phase_started_at"
  phase_started_at=$(timestamp_now); phase4b_vault; record_phase_duration phase4b_vault "$phase_started_at"
  phase_started_at=$(timestamp_now); phase5_eso; record_phase_duration phase5_eso "$phase_started_at"
  phase_started_at=$(timestamp_now); phase6_operators; record_phase_duration phase6_operators "$phase_started_at"
  phase_started_at=$(timestamp_now); phase6b_observability; record_phase_duration phase6b_observability "$phase_started_at"
  phase_started_at=$(timestamp_now); phase7_platform; record_phase_duration phase7_platform "$phase_started_at"
  phase_started_at=$(timestamp_now); phase7b_headlamp; record_phase_duration phase7b_headlamp "$phase_started_at"
  phase_started_at=$(timestamp_now); phase8_stateful; record_phase_duration phase8_stateful "$phase_started_at"
  phase_started_at=$(timestamp_now); phase9_services; record_phase_duration phase9_services "$phase_started_at"
  phase_started_at=$(timestamp_now); phase10_wait_smoke; record_phase_duration phase10_wait_smoke "$phase_started_at"
  print_phase_summary
  log "INFO" "End OK"
}

main "$@"
