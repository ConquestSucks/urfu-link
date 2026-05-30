param(
    [string]$SshTarget = "root@89.167.93.199",
    [switch]$DirectApply
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

function Invoke-Remote {
    param([string]$Command)
    ssh -o BatchMode=yes $SshTarget $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Remote command failed with exit code ${LASTEXITCODE}: $Command"
    }
}

function Invoke-RemoteScript {
    param([string]$Script)
    $normalizedScript = $Script.Replace("`r`n", "`n").Replace("`r", "`n")
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($normalizedScript))
    Invoke-Remote "printf '%s' '$encoded' | base64 -d | bash"
}

if ($DirectApply) {
    Write-Host "[media] Directly applying deploy/k8s/platform to the cluster." -ForegroundColor Yellow
    Write-Host "[media] Use this only for emergency rollout; merge to master to keep ArgoCD in sync." -ForegroundColor Yellow
    $vaultBootstrapPath = Join-Path $repoRoot "deploy/k8s/platform/vault/vault-auto-init-job.yaml"
    $remoteVaultBootstrap = "/tmp/urfu-vault-auto-init-job.yaml"
    scp -q $vaultBootstrapPath "${SshTarget}:$remoteVaultBootstrap"
    Invoke-Remote "kubectl delete job vault-init -n vault --ignore-not-found=true; kubectl apply -f $remoteVaultBootstrap; rm -f $remoteVaultBootstrap; kubectl wait --for=condition=complete job/vault-init -n vault --timeout=300s"

    $statefulManifestPath = Join-Path $env:TEMP ("urfu-platform-stateful-{0}.yaml" -f ([Guid]::NewGuid().ToString("N")))
    $statefulManifestContent = kubectl kustomize (Join-Path $repoRoot "deploy/k8s/platform/stateful")
    [System.IO.File]::WriteAllText(
        $statefulManifestPath,
        ($statefulManifestContent -join [Environment]::NewLine),
        [System.Text.UTF8Encoding]::new($false))
    if ((Get-Item $statefulManifestPath).Length -eq 0) {
        throw "kubectl kustomize returned an empty stateful manifest."
    }
    $remoteStatefulManifest = "/tmp/urfu-platform-stateful-media.yaml"
    scp -q $statefulManifestPath "${SshTarget}:$remoteStatefulManifest"
    Invoke-Remote "kubectl delete job platform-bootstrap -n urfu-platform --ignore-not-found=true"
    Invoke-Remote "kubectl apply -f $remoteStatefulManifest; rm -f $remoteStatefulManifest"
    Remove-Item -LiteralPath $statefulManifestPath -Force
    Invoke-Remote "kubectl wait --for=condition=complete job/platform-bootstrap -n urfu-platform --timeout=300s"

    $manifestPath = Join-Path $env:TEMP ("urfu-platform-{0}.yaml" -f ([Guid]::NewGuid().ToString("N")))
    $manifestContent = kubectl kustomize (Join-Path $repoRoot "deploy/k8s/platform")
    [System.IO.File]::WriteAllText(
        $manifestPath,
        ($manifestContent -join [Environment]::NewLine),
        [System.Text.UTF8Encoding]::new($false))
    if ((Get-Item $manifestPath).Length -eq 0) {
        throw "kubectl kustomize returned an empty manifest."
    }
    $remoteManifest = "/tmp/urfu-platform-media.yaml"
    scp -q $manifestPath "${SshTarget}:$remoteManifest"
    Invoke-Remote "kubectl apply -f $remoteManifest; rm -f $remoteManifest"
    Remove-Item -LiteralPath $manifestPath -Force
}
else {
    Write-Host "[media] Requesting ArgoCD refresh. Automated sync must apply master." -ForegroundColor Cyan
    Invoke-Remote "kubectl annotate app platform-stateful -n argocd argocd.argoproj.io/refresh=hard --overwrite >/dev/null || true"
    Invoke-Remote "kubectl annotate app platform-manifests -n argocd argocd.argoproj.io/refresh=hard --overwrite >/dev/null || true"
    Invoke-Remote "kubectl annotate app call-service-prod -n argocd argocd.argoproj.io/refresh=hard --overwrite >/dev/null || true"
}

Write-Host "[media] Ensuring host firewall allows LiveKit media ports." -ForegroundColor Cyan
Invoke-Remote "ufw allow 7881/tcp >/dev/null; ufw allow 3478/udp >/dev/null; ufw allow 5349/tcp >/dev/null; ufw allow 50000:50100/udp >/dev/null; ufw allow 50101:50200/udp >/dev/null"

Write-Host "[media] Requesting External Secrets refresh." -ForegroundColor Cyan
Invoke-Remote "timeout 300 sh -c 'until kubectl get externalsecret livekit-secrets -n urfu-platform >/dev/null 2>&1; do sleep 5; done; until kubectl get externalsecret call-service-secrets -n urfu-prod >/dev/null 2>&1; do sleep 5; done; until kubectl get certificate livekit-turn-tls -n urfu-platform >/dev/null 2>&1; do sleep 5; done'"
Invoke-Remote 'stamp=$(date +%s); kubectl annotate externalsecret livekit-secrets -n urfu-platform force-sync=$stamp --overwrite >/dev/null 2>&1 || true; kubectl annotate externalsecret call-service-secrets -n urfu-prod force-sync=$stamp --overwrite >/dev/null 2>&1 || true'
Invoke-Remote "kubectl wait --for=condition=Ready externalsecret/livekit-secrets -n urfu-platform --timeout=120s; kubectl wait --for=condition=Ready externalsecret/call-service-secrets -n urfu-prod --timeout=120s"
Invoke-Remote "kubectl wait --for=condition=Ready certificate/livekit-turn-tls -n urfu-platform --timeout=300s"

Write-Host "[media] Removing legacy CoTURN resources if they exist." -ForegroundColor Cyan
Invoke-Remote "kubectl delete deploy,svc,cm coturn -n urfu-platform --ignore-not-found=true"

Write-Host "[media] Restarting LiveKit and call-service." -ForegroundColor Cyan
Invoke-Remote "kubectl rollout restart deploy/livekit -n urfu-platform"
$restartAt = (Get-Date).ToUniversalTime().ToString("o")
$callServiceRestartScript = @"
set -eu
kubectl patch rollout call-service -n urfu-prod --type merge --patch '{"spec":{"restartAt":"$restartAt"}}'
"@
Invoke-RemoteScript $callServiceRestartScript

Write-Host "[media] Waiting for readiness." -ForegroundColor Cyan
Invoke-Remote "kubectl rollout status deploy/livekit -n urfu-platform --timeout=180s"
Invoke-Remote "kubectl wait --for=condition=ready pod -n urfu-platform -l app=livekit --timeout=180s"
Invoke-Remote "kubectl wait --for=condition=ready pod -n urfu-prod -l app.kubernetes.io/name=call-service --timeout=300s"

Write-Host "[media] Media switch completed." -ForegroundColor Green
