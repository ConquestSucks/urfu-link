param(
    [string]$ClusterName = "urfu-link"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\\local-k8s-prereqs.ps1"

Test-CommandAvailable -Name "kind" -InstallHint "Install kind: https://kind.sigs.k8s.io/"

Write-Host "[urfu-link] Deleting local Kubernetes cluster $ClusterName..." -ForegroundColor Yellow
kind delete cluster --name $ClusterName
