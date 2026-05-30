param(
    [string]$SshTarget = "root@89.167.93.199",
    [string]$ExpectedIp = "89.167.93.199",
    [string]$LiveKitUrl = "https://livekit.urfu-link.ghjc.ru",
    [string]$ApiUrl = "https://api.urfu-link.ghjc.ru",
    [string]$CallId,
    [string]$AccessToken
)

$ErrorActionPreference = "Stop"

function Invoke-Remote {
    param([string]$Command)
    ssh -o BatchMode=yes $SshTarget $Command
}

Write-Host "[smoke] Checking LiveKit HTTPS endpoint." -ForegroundColor Cyan
$statusCode = $null
try {
    $response = Invoke-WebRequest -Uri $LiveKitUrl -Method Head -TimeoutSec 10 -UseBasicParsing
    $statusCode = [int]$response.StatusCode
}
catch {
    if ($_.Exception.Response) {
        $statusCode = [int]$_.Exception.Response.StatusCode
    }
    else {
        throw
    }
}
Write-Host "[smoke] $LiveKitUrl -> HTTP $statusCode"

foreach ($port in @(443, 7881, 5349)) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.BeginConnect($ExpectedIp, $port, $null, $null)
        $connected = $connect.AsyncWaitHandle.WaitOne(3000)
        if (-not $connected) {
            throw "TCP $port timed out"
        }
        $client.EndConnect($connect)
        Write-Host "[smoke] TCP $port reachable" -ForegroundColor Green
    }
    finally {
        $client.Close()
    }
}

Write-Host "[smoke] Checking server-side listeners and pods." -ForegroundColor Cyan
Invoke-Remote "kubectl get pod -n urfu-platform -l app=livekit; kubectl get pod -n urfu-platform -l app=coturn; ss -lntup | grep -E ':(7880|7881|3478|5349)\b' || true"

if ($CallId -and $AccessToken) {
    Write-Host "[smoke] Checking call token serverUrl." -ForegroundColor Cyan
    $headers = @{ Authorization = "Bearer $AccessToken" }
    $tokenResponse = Invoke-RestMethod -Uri "$ApiUrl/api/calls/$CallId/token" -Headers $headers -Method Post -TimeoutSec 20
    if ($tokenResponse.serverUrl -ne "wss://livekit.urfu-link.ghjc.ru") {
        throw "Unexpected call token serverUrl: $($tokenResponse.serverUrl)"
    }
    Write-Host "[smoke] call token serverUrl is correct" -ForegroundColor Green
}
else {
    Write-Host "[smoke] Skipping call token check; pass -CallId and -AccessToken to verify serverUrl." -ForegroundColor Yellow
}

Write-Host "[smoke] Production media smoke completed." -ForegroundColor Green
