param(
    [string]$SshTarget = "root@89.167.93.199",
    [string]$ExpectedIp = "89.167.93.199",
    [string[]]$MediaDomains = @("livekit.urfu-link.ghjc.ru", "turn.urfu-link.ghjc.ru"),
    [switch]$RequireTargetState
)

$ErrorActionPreference = "Stop"
$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([string]$Message)
    $failures.Add($Message) | Out-Null
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Add-Ok {
    param([string]$Message)
    Write-Host "[ OK ] $Message" -ForegroundColor Green
}

function Add-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Add-TargetFinding {
    param([string]$Message)

    if ($RequireTargetState) {
        Add-Failure $Message
    }
    else {
        Add-Warn $Message
    }
}

function Test-TcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutMs = 3000
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $connect.AsyncWaitHandle.WaitOne($TimeoutMs)) {
            return $false
        }

        $client.EndConnect($connect)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Close()
    }
}

function Invoke-RemoteScript {
    param([string]$Script)
    $normalizedScript = $Script.Replace("`r`n", "`n").Replace("`r", "`n")
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($normalizedScript))
    ssh -o BatchMode=yes $SshTarget "printf '%s' '$encoded' | base64 -d | bash"
}

foreach ($domain in $MediaDomains) {
    $addresses = @(Resolve-DnsName $domain -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Type -eq "A" -and
            ((-not ($_.PSObject.Properties.Name -contains "Section")) -or $_.Section -eq "Answer")
        } |
        Select-Object -ExpandProperty IPAddress -Unique)

    if ($addresses.Count -eq 1 -and $addresses[0] -eq $ExpectedIp) {
        Add-Ok "$domain resolves only to $ExpectedIp"
    }
    else {
        Add-Failure "$domain must resolve only to $ExpectedIp; current: $($addresses -join ', ')"
    }
}

if (Test-TcpPort -HostName $ExpectedIp -Port 443) {
    Add-Ok "TCP 443 reachable from this machine"
}
else {
    Add-Failure "TCP 443 is not reachable from this machine"
}

if ($RequireTargetState) {
    foreach ($port in @(7881, 5349)) {
        if (Test-TcpPort -HostName $ExpectedIp -Port $port) {
            Add-Ok "TCP $port reachable from this machine"
        }
        else {
            Add-Failure "TCP $port is not reachable from this machine"
        }
    }
}
else {
    Add-Warn "Skipping external TCP 7881/5349 reachability; run with -RequireTargetState after switch."
}

$placeholderPattern = "replace" + "-me|dev" + "key|dev" + "secret"
$repoScan = rg -n $placeholderPattern deploy/k8s platform src apps 2>$null
if ($LASTEXITCODE -eq 0 -and $repoScan) {
    Add-Failure "Repository still contains forbidden placeholder LiveKit values:`n$repoScan"
}
else {
    Add-Ok "Repository has no forbidden LiveKit placeholder values"
}

$remoteScript = @'
set -eu

kv() {
  printf '%s=%s\n' "$1" "$2"
}

secret_keys() {
  ns="$1"
  name="$2"
  if kubectl get secret "$name" -n "$ns" >/dev/null 2>&1; then
    kubectl get secret "$name" -n "$ns" -o json > "/tmp/${ns}-${name}.json"
    keys="$(python3 - "/tmp/${ns}-${name}.json" <<'PY'
import json, sys
with open(sys.argv[1]) as f:
    obj = json.load(f)
print(",".join(sorted(obj.get("data", {}).keys())))
PY
)"
    rm -f "/tmp/${ns}-${name}.json"
    kv "SECRET_${ns}_${name}" "present:${keys}"
  else
    kv "SECRET_${ns}_${name}" "missing"
  fi
}

external_secret_ready() {
  ns="$1"
  name="$2"
  if kubectl get externalsecret "$name" -n "$ns" >/dev/null 2>&1; then
    ready="$(kubectl get externalsecret "$name" -n "$ns" -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || true)"
    reason="$(kubectl get externalsecret "$name" -n "$ns" -o jsonpath='{.status.conditions[?(@.type=="Ready")].reason}' 2>/dev/null || true)"
    kv "EXTERNALSECRET_${ns}_${name}" "present:${ready}:${reason}"
  else
    kv "EXTERNALSECRET_${ns}_${name}" "missing"
  fi
}

resource_count() {
  ns="$1"
  selector="$2"
  kind="$3"
  count="$(kubectl get "$kind" -n "$ns" -l "$selector" --no-headers 2>/dev/null | wc -l | tr -d ' ')"
  kv "${kind}_${ns}_${selector}" "$count"
}

livekit_count="$(kubectl get pod -n urfu-platform -l app=livekit --no-headers 2>/dev/null | wc -l | tr -d ' ')"
coturn_count="$(kubectl get pod -n urfu-platform -l app=coturn --no-headers 2>/dev/null | wc -l | tr -d ' ')"
kv PODCOUNT_LIVEKIT "$livekit_count"
kv PODCOUNT_COTURN "$coturn_count"

secret_keys urfu-platform livekit-secrets
secret_keys urfu-prod call-service-secrets
external_secret_ready urfu-platform livekit-secrets
external_secret_ready urfu-prod call-service-secrets

if kubectl get clustersecretstore vault-backend >/dev/null 2>&1; then
  store_ready="$(kubectl get clustersecretstore vault-backend -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || true)"
  kv CLUSTERSECRETSTORE_vault-backend "$store_ready"
else
  kv CLUSTERSECRETSTORE_vault-backend missing
fi

if kubectl get cm livekit-config -n urfu-platform >/dev/null 2>&1; then
  kubectl get cm livekit-config -n urfu-platform -o json > /tmp/livekit-config.json
  python3 - /tmp/livekit-config.json <<'PY'
import json, re, sys
with open(sys.argv[1]) as f:
    obj = json.load(f)
text = obj.get("data", {}).get("livekit.yaml", "")
print("LIVEKIT_CONFIG_INLINE_KEYS=" + str(bool(re.search(r"(?m)^keys:", text))).lower())
for token in [
    "use_external_ip: true",
    "tcp_port: 7881",
    "port_range_start: 50000",
    "port_range_end: 50100",
    "turn:",
    "enabled: true",
    "domain: turn.urfu-link.ghjc.ru",
    "udp_port: 3478",
    "tls_port: 5349",
    "relay_range_start: 50101",
    "relay_range_end: 50200",
]:
    print("LIVEKIT_CONFIG_HAS_" + re.sub(r"[^A-Za-z0-9]+", "_", token).strip("_").upper() + "=" + str(token in text).lower())
PY
  rm -f /tmp/livekit-config.json
else
  kv LIVEKIT_CONFIG missing
fi

if kubectl get deploy livekit -n urfu-platform >/dev/null 2>&1; then
  kubectl get deploy livekit -n urfu-platform -o json > /tmp/livekit-deploy.json
  python3 - /tmp/livekit-deploy.json <<'PY'
import json, sys
with open(sys.argv[1]) as f:
    obj = json.load(f)
spec = obj["spec"]["template"]["spec"]
container = spec["containers"][0]
env_from = container.get("envFrom", [])
mounts = [m.get("name") for m in container.get("volumeMounts", [])]
volumes = [v.get("name") for v in spec.get("volumes", [])]
print("LIVEKIT_DEPLOY_HOSTNETWORK=" + str(spec.get("hostNetwork")).lower())
print("LIVEKIT_DEPLOY_ENVFROM_LIVEKIT_SECRETS=" + str(any(e.get("secretRef", {}).get("name") == "livekit-secrets" for e in env_from)).lower())
print("LIVEKIT_DEPLOY_MOUNT_TURN_TLS=" + str("livekit-turn-tls" in mounts).lower())
print("LIVEKIT_DEPLOY_VOLUME_TURN_TLS=" + str("livekit-turn-tls" in volumes).lower())
PY
  rm -f /tmp/livekit-deploy.json
else
  kv LIVEKIT_DEPLOY missing
fi

if kubectl get certificate livekit-turn-tls -n urfu-platform >/dev/null 2>&1; then
  cert_ready="$(kubectl get certificate livekit-turn-tls -n urfu-platform -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || true)"
  kv CERTIFICATE_livekit-turn-tls "$cert_ready"
else
  kv CERTIFICATE_livekit-turn-tls missing
fi

if kubectl get ingress livekit -n urfu-platform >/dev/null 2>&1; then
  hosts="$(kubectl get ingress livekit -n urfu-platform -o jsonpath='{.spec.rules[*].host}' 2>/dev/null || true)"
  kv INGRESS_livekit "$hosts"
else
  kv INGRESS_livekit missing
fi

for app in platform-stateful platform-manifests call-service-prod frontend-web-prod; do
  if kubectl get app "$app" -n argocd >/dev/null 2>&1; then
    sync="$(kubectl get app "$app" -n argocd -o jsonpath='{.status.sync.status}' 2>/dev/null || true)"
    health="$(kubectl get app "$app" -n argocd -o jsonpath='{.status.health.status}' 2>/dev/null || true)"
    kv "ARGO_${app}" "${sync}:${health}"
  else
    kv "ARGO_${app}" missing
  fi
done

ufw_status="$(ufw status verbose || true)"
printf '%s\n' "$ufw_status" > /tmp/ufw-status.txt
python3 - /tmp/ufw-status.txt <<'PY'
import re, sys
text = open(sys.argv[1]).read()
checks = {
    "UFW_7881_TCP": r"(?m)^7881/tcp\s+ALLOW",
    "UFW_3478_UDP": r"(?m)^3478/udp\s+ALLOW",
    "UFW_5349_TCP": r"(?m)^5349/tcp\s+ALLOW",
    "UFW_50000_50100_UDP": r"(?m)^50000(?::|-)?50100/udp\s+ALLOW",
    "UFW_50101_50200_UDP": r"(?m)^50101(?::|-)?50200/udp\s+ALLOW",
}
for name, pattern in checks.items():
    print(f"{name}={str(bool(re.search(pattern, text))).lower()}")
PY
rm -f /tmp/ufw-status.txt
'@

$remoteOutput = Invoke-RemoteScript $remoteScript
$remoteOutput | ForEach-Object { Write-Host $_ }

$remote = @{}
foreach ($line in $remoteOutput) {
    $parts = $line -split "=", 2
    if ($parts.Count -eq 2) {
        $remote[$parts[0]] = $parts[1]
    }
}

foreach ($portCheck in @("UFW_7881_TCP", "UFW_3478_UDP", "UFW_5349_TCP", "UFW_50000_50100_UDP", "UFW_50101_50200_UDP")) {
    if ($remote[$portCheck] -eq "true") {
        Add-Ok "$portCheck is allowed"
    }
    else {
        Add-Failure "$portCheck is missing"
    }
}

foreach ($app in @("platform-stateful", "platform-manifests", "call-service-prod", "frontend-web-prod")) {
    $status = $remote["ARGO_$app"]
    if ($status -eq "Synced:Healthy") {
        Add-Ok "ArgoCD $app is Synced/Healthy"
    }
    else {
        Add-Failure "ArgoCD $app is not Synced/Healthy: $status"
    }
}

if ($remote["CLUSTERSECRETSTORE_vault-backend"] -eq "True") {
    Add-Ok "ClusterSecretStore vault-backend is ready"
}
else {
    Add-Failure "ClusterSecretStore vault-backend is not ready: $($remote["CLUSTERSECRETSTORE_vault-backend"])"
}

$livekitSecret = [string]$remote["SECRET_urfu-platform_livekit-secrets"]
if ($livekitSecret -like "present:*" -and $livekitSecret -match "LIVEKIT_KEYS") {
    Add-Ok "livekit-secrets exposes LIVEKIT_KEYS"
}
else {
    Add-TargetFinding "livekit-secrets is not ready with LIVEKIT_KEYS yet: $livekitSecret"
}

$callServiceSecret = [string]$remote["SECRET_urfu-prod_call-service-secrets"]
foreach ($requiredKey in @("LiveKit__ServerUrl", "LiveKit__ApiKey", "LiveKit__ApiSecret")) {
    if ($callServiceSecret -match [regex]::Escape($requiredKey)) {
        Add-Ok "call-service-secrets exposes $requiredKey"
    }
    else {
        Add-TargetFinding "call-service-secrets does not expose $requiredKey yet"
    }
}

if ($remote["EXTERNALSECRET_urfu-platform_livekit-secrets"] -like "present:True:*") {
    Add-Ok "ExternalSecret livekit-secrets is ready"
}
else {
    Add-TargetFinding "ExternalSecret livekit-secrets is not ready yet: $($remote["EXTERNALSECRET_urfu-platform_livekit-secrets"])"
}

if ($remote["EXTERNALSECRET_urfu-prod_call-service-secrets"] -like "present:True:*") {
    Add-Ok "ExternalSecret call-service-secrets is ready"
}
else {
    Add-Failure "ExternalSecret call-service-secrets is not ready: $($remote["EXTERNALSECRET_urfu-prod_call-service-secrets"])"
}

if ($remote["PODCOUNT_LIVEKIT"] -eq "1") {
    Add-Ok "Exactly one LiveKit pod exists"
}
else {
    Add-Failure "Unexpected LiveKit pod count: $($remote["PODCOUNT_LIVEKIT"])"
}

if ($remote["PODCOUNT_COTURN"] -eq "0") {
    Add-Ok "CoTURN is absent from active prod path"
}
else {
    Add-TargetFinding "CoTURN still exists before media switch: $($remote["PODCOUNT_COTURN"]) pod(s)"
}

if ($remote["LIVEKIT_CONFIG_INLINE_KEYS"] -eq "false") {
    Add-Ok "LiveKit config has no inline API keys"
}
else {
    Add-TargetFinding "LiveKit config still has inline API keys before media switch"
}

foreach ($configCheck in @(
    "LIVEKIT_CONFIG_HAS_USE_EXTERNAL_IP_TRUE",
    "LIVEKIT_CONFIG_HAS_TCP_PORT_7881",
    "LIVEKIT_CONFIG_HAS_PORT_RANGE_START_50000",
    "LIVEKIT_CONFIG_HAS_PORT_RANGE_END_50100",
    "LIVEKIT_CONFIG_HAS_TURN",
    "LIVEKIT_CONFIG_HAS_ENABLED_TRUE",
    "LIVEKIT_CONFIG_HAS_DOMAIN_TURN_URFU_LINK_GHJC_RU",
    "LIVEKIT_CONFIG_HAS_UDP_PORT_3478",
    "LIVEKIT_CONFIG_HAS_TLS_PORT_5349",
    "LIVEKIT_CONFIG_HAS_RELAY_RANGE_START_50101",
    "LIVEKIT_CONFIG_HAS_RELAY_RANGE_END_50200",
    "LIVEKIT_DEPLOY_HOSTNETWORK",
    "LIVEKIT_DEPLOY_ENVFROM_LIVEKIT_SECRETS",
    "LIVEKIT_DEPLOY_MOUNT_TURN_TLS",
    "LIVEKIT_DEPLOY_VOLUME_TURN_TLS"
)) {
    if ($remote[$configCheck] -eq "true") {
        Add-Ok "$configCheck target is present"
    }
    else {
        Add-TargetFinding "$configCheck target is not present yet"
    }
}

if ($remote["CERTIFICATE_livekit-turn-tls"] -eq "True") {
    Add-Ok "TURN TLS certificate is ready"
}
else {
    Add-TargetFinding "TURN TLS certificate is not ready yet: $($remote["CERTIFICATE_livekit-turn-tls"])"
}

if ($remote["INGRESS_livekit"] -eq "livekit.urfu-link.ghjc.ru") {
    Add-Ok "LiveKit ingress host is configured"
}
else {
    Add-TargetFinding "LiveKit ingress is not configured yet: $($remote["INGRESS_livekit"])"
}

if ($failures.Count -gt 0) {
    Write-Host "`nPreflight failed with $($failures.Count) issue(s)." -ForegroundColor Red
    exit 1
}

Write-Host "`nPreflight passed." -ForegroundColor Green
