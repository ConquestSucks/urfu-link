param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("api-gateway", "media-service", "user-service", "chat-service", "presence-service", "notification-service", "call-service", "frontend-web")]
    [string]$Service,

    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "prod")]
    [string]$Environment,

    [string]$Namespace
)

$ErrorActionPreference = "Stop"

if (-not $Namespace) {
    $Namespace = if ($Environment -eq "prod") { "urfu-prod" } else { "urfu-dev" }
}

$valuesFile = "deploy/helm/services/$Service/values-$Environment.yaml"

Write-Host "[urfu-link] Deploying $Service to $Namespace with $valuesFile" -ForegroundColor Cyan
helm upgrade --install $Service deploy/helm/charts/urfu-service -n $Namespace --create-namespace -f $valuesFile

Write-Host "[urfu-link] Deployment command completed." -ForegroundColor Green
