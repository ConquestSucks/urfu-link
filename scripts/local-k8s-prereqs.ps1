function Get-CommandOrNull {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return Get-Command $Name -ErrorAction SilentlyContinue
}

function Test-CommandAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$InstallHint
    )

    $command = Get-CommandOrNull -Name $Name
    if ($null -eq $command) {
        throw "Required tool '$Name' is not installed or is not available in PATH. $InstallHint"
    }
}
