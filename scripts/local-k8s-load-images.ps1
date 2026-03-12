param(
    [string]$ClusterName = "urfu-link"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\\local-k8s-prereqs.ps1"

Test-CommandAvailable -Name "docker" -InstallHint "Install Docker Desktop and ensure the 'docker' CLI is available in PATH."
Test-CommandAvailable -Name "kind" -InstallHint "Install kind: https://kind.sigs.k8s.io/"

$images = @(
    @{ Service = "api-gateway"; Dockerfile = "src/Gateway/ApiGateway/Dockerfile" },
    @{ Service = "media-service"; Dockerfile = "src/Services/Media/MediaService.Api/Dockerfile" },
    @{ Service = "user-service"; Dockerfile = "src/Services/User/UserService.Api/Dockerfile" },
    @{ Service = "chat-service"; Dockerfile = "src/Services/Chat/ChatService.Api/Dockerfile" },
    @{ Service = "presence-service"; Dockerfile = "src/Services/Presence/PresenceService.Api/Dockerfile" },
    @{ Service = "notification-service"; Dockerfile = "src/Services/Notification/NotificationService.Api/Dockerfile" },
    @{ Service = "call-service"; Dockerfile = "src/Services/Call/CallService.Api/Dockerfile" },
    @{ Service = "frontend-web"; Dockerfile = "apps/client/Dockerfile" }
)

foreach ($image in $images) {
    $tag = if ($image.Service -eq "frontend-web") { "ghcr.io/urfu-link/frontend-web:dev-local" } else { "urfu-link/$($image.Service):dev-local" }
    Write-Host "[urfu-link] Building $tag" -ForegroundColor Cyan
    docker build -f $image.Dockerfile -t $tag .
    kind load docker-image $tag --name $ClusterName
}
