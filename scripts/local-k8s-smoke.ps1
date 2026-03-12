param(
    [string]$Namespace = "urfu-dev"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\\local-k8s-prereqs.ps1"

Test-CommandAvailable -Name "kubectl" -InstallHint "Install kubectl: https://kubernetes.io/docs/tasks/tools/"

$services = @(
    "api-gateway",
    "user-service",
    "media-service",
    "chat-service",
    "presence-service",
    "notification-service",
    "call-service",
    "frontend-web"
)

foreach ($service in $services) {
    kubectl wait --for=condition=ready --timeout=300s pod -l "app.kubernetes.io/name=$service" -n $Namespace
}

kubectl get ingress api-gateway -n $Namespace
kubectl get ingress frontend-web -n $Namespace
