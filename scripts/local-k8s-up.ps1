param(
    [switch]$BuildImages,
    [switch]$InstallLinkerd,
    [string]$ClusterName = "urfu-link"
)

$ErrorActionPreference = "Stop"

function Invoke-ManifestApply {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string]$ErrorMessage
    )

    $manifest = Invoke-Expression $Command | Out-String
    if ([string]::IsNullOrWhiteSpace($manifest)) {
        throw $ErrorMessage
    }

    $manifest | kubectl apply -f -
}

. "$PSScriptRoot\\local-k8s-prereqs.ps1"

Test-CommandAvailable -Name "kind" -InstallHint "Install kind: https://kind.sigs.k8s.io/"
Test-CommandAvailable -Name "kubectl" -InstallHint "Install kubectl: https://kubernetes.io/docs/tasks/tools/"
Test-CommandAvailable -Name "helm" -InstallHint "Install Helm: https://helm.sh/docs/intro/install/"

if ($BuildImages) {
    Test-CommandAvailable -Name "docker" -InstallHint "Install Docker Desktop and ensure the 'docker' CLI is available in PATH."
}

if ($InstallLinkerd) {
    Test-CommandAvailable -Name "linkerd" -InstallHint "Install Linkerd CLI: https://linkerd.io/2/getting-started/"
}

$clusterExists = kind get clusters | Where-Object { $_ -eq $ClusterName }
if (-not $clusterExists) {
    Write-Host "[urfu-link] Creating kind cluster $ClusterName..." -ForegroundColor Cyan
    kind create cluster --name $ClusterName --config platform/dev/local-k8s/kind-config.yaml
}

Write-Host "[urfu-link] Installing ingress-nginx..." -ForegroundColor Cyan
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml
kubectl wait --namespace ingress-nginx --for=condition=available deployment/ingress-nginx-controller --timeout=300s

Write-Host "[urfu-link] Configuring CoreDNS for local kind cluster..." -ForegroundColor Cyan
kubectl apply -f platform/dev/local-k8s/coredns-config.yaml
kubectl rollout restart deployment/coredns -n kube-system
kubectl wait --namespace kube-system --for=condition=available deployment/coredns --timeout=300s

if ($InstallLinkerd) {
    Write-Host "[urfu-link] Installing Gateway API CRDs..." -ForegroundColor Cyan
    kubectl apply --server-side -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.4.0/standard-install.yaml

    $linkerdInstalled = $null -ne (kubectl get configmap linkerd-config -n linkerd --ignore-not-found -o name)

    if ($linkerdInstalled) {
        Write-Host "[urfu-link] Upgrading Linkerd CRDs..." -ForegroundColor Cyan
        Invoke-ManifestApply -Command "linkerd upgrade --crds" -ErrorMessage "Linkerd CRD upgrade returned an empty manifest."

        Write-Host "[urfu-link] Upgrading Linkerd control plane..." -ForegroundColor Cyan
        Invoke-ManifestApply -Command "linkerd upgrade" -ErrorMessage "Linkerd control plane upgrade returned an empty manifest."
    }
    else {
        Write-Host "[urfu-link] Installing Linkerd CRDs..." -ForegroundColor Cyan
        Invoke-ManifestApply -Command "linkerd install --crds" -ErrorMessage "Linkerd CRD installation returned an empty manifest."

        Write-Host "[urfu-link] Installing Linkerd control plane..." -ForegroundColor Cyan
        Invoke-ManifestApply -Command "linkerd install" -ErrorMessage "Linkerd control plane installation returned an empty manifest."
    }

    kubectl wait --namespace linkerd --for=condition=available deployment/linkerd-destination --timeout=300s
    kubectl wait --namespace linkerd --for=condition=available deployment/linkerd-identity --timeout=300s
    kubectl wait --namespace linkerd --for=condition=available deployment/linkerd-proxy-injector --timeout=300s

    Write-Host "[urfu-link] Installing Linkerd Viz..." -ForegroundColor Cyan
    Invoke-ManifestApply -Command "linkerd viz install" -ErrorMessage "Linkerd Viz installation returned an empty manifest."
    kubectl wait --namespace linkerd-viz --for=condition=available deployment/metrics-api --timeout=300s
    kubectl wait --namespace linkerd-viz --for=condition=available deployment/web --timeout=300s

    Write-Host "[urfu-link] Verifying Linkerd..." -ForegroundColor Cyan
    linkerd check
    linkerd viz check
}

Write-Host "[urfu-link] Applying namespaces..." -ForegroundColor Cyan
kubectl apply -f deploy/k8s/platform/namespaces.yaml

Write-Host "[urfu-link] Creating local config maps..." -ForegroundColor Cyan
kubectl create configmap postgres-init -n urfu-platform --from-file=platform/dev/local-k8s/postgres-init.sql --dry-run=client -o yaml | kubectl apply -f -
kubectl create configmap keycloak-realm -n urfu-platform --from-file=realm-urfu-link.json=platform/dev/keycloak/realm-urfu-link.json --dry-run=client -o yaml | kubectl apply -f -
kubectl create configmap livekit-config -n urfu-platform --from-file=livekit.yaml=platform/dev/livekit/livekit.yaml --dry-run=client -o yaml | kubectl apply -f -
kubectl create configmap coturn-config -n urfu-platform --from-file=turnserver.conf=platform/dev/coturn/turnserver.conf --dry-run=client -o yaml | kubectl apply -f -
kubectl create configmap otel-collector-config -n observability --from-file=config.yaml=platform/dev/otel/otel-collector-config.yaml --dry-run=client -o yaml | kubectl apply -f -

Write-Host "[urfu-link] Applying local platform resources..." -ForegroundColor Cyan
kubectl delete job postgres-bootstrap -n urfu-platform --ignore-not-found=true
kubectl delete job kafka-bootstrap -n urfu-platform --ignore-not-found=true
kubectl apply -k platform/dev/local-k8s
kubectl wait --namespace urfu-platform --for=condition=available deployment/postgres --timeout=300s
kubectl wait --namespace urfu-platform --for=condition=available deployment/kafka --timeout=300s
kubectl wait --namespace urfu-platform --for=condition=complete job/postgres-bootstrap --timeout=300s
kubectl wait --namespace urfu-platform --for=condition=complete job/kafka-bootstrap --timeout=300s

if ($BuildImages) {
    & .\scripts\local-k8s-load-images.ps1 -ClusterName $ClusterName
}

$services = @(
    "api-gateway",
    "media-service",
    "user-service",
    "chat-service",
    "presence-service",
    "notification-service",
    "call-service",
    "frontend-web"
)

foreach ($service in $services) {
    $valuesFile = "deploy/helm/services/$service/values-dev.yaml"
    Write-Host "[urfu-link] Deploying $service with $valuesFile" -ForegroundColor Cyan
    helm upgrade --install $service deploy/helm/charts/urfu-service -n urfu-dev --create-namespace -f $valuesFile
}

& .\scripts\local-k8s-smoke.ps1

Write-Host "[urfu-link] Local Kubernetes environment is ready." -ForegroundColor Green
