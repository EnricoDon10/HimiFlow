[CmdletBinding()]
param(
    [string]$PublishRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\api")
)

$ErrorActionPreference = "Stop"
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($PublishRoot)
$apiAssembly = Join-Path $resolvedPublishRoot "Einsparungs.Api.dll"

if (-not (Test-Path -LiteralPath $apiAssembly -PathType Leaf)) {
    throw "Die veröffentlichte API wurde nicht gefunden: $apiAssembly"
}

Push-Location $resolvedPublishRoot
try {
    dotnet .\Einsparungs.Api.dll --backup-now
    if ($LASTEXITCODE -ne 0) {
        throw "Das manuelle SQLite-Backup ist fehlgeschlagen."
    }
}
finally {
    Pop-Location
}
