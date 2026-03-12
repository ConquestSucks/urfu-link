param(
    [switch]$IncludeStateful
)

$ErrorActionPreference = "Stop"

Write-Host "[urfu-link] Applying namespaces..." -ForegroundColor Cyan
kubectl apply -f deploy/k8s/platform/namespaces.yaml

Write-Host "[urfu-link] Applying platform baseline (kustomize)..." -ForegroundColor Cyan
kubectl apply -k deploy/k8s/platform

if ($IncludeStateful) {
    Write-Host "[urfu-link] Applying self-hosted stateful stack (kustomize)..." -ForegroundColor Cyan
    kubectl apply -k deploy/k8s/platform/stateful
}

Write-Host "[urfu-link] On-prem bootstrap completed." -ForegroundColor Green
