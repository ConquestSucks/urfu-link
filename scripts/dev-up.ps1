param(
    [switch]$Build,
    [switch]$InstallLinkerd
)

$ErrorActionPreference = "Stop"

& .\scripts\local-k8s-up.ps1 -BuildImages:$Build -InstallLinkerd:$InstallLinkerd
