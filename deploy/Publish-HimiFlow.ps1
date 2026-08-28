[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts")
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$backendProject = Join-Path $repositoryRoot "backend\Einsparungs.Api\Einsparungs.Api.csproj"
$frontendRoot = Join-Path $repositoryRoot "frontend"
$apiOutput = Join-Path $OutputRoot "api"
$frontendOutput = Join-Path $OutputRoot "frontend"

New-Item -ItemType Directory -Force -Path $apiOutput, $frontendOutput | Out-Null

dotnet publish $backendProject --configuration Release --output $apiOutput
if ($LASTEXITCODE -ne 0) {
    throw "Der Backend-Publish ist fehlgeschlagen."
}

Push-Location $frontendRoot
try {
    npm ci
    if ($LASTEXITCODE -ne 0) {
        throw "npm ci ist fehlgeschlagen."
    }

    npm run build -- --configuration production
    if ($LASTEXITCODE -ne 0) {
        throw "Der Frontend-Build ist fehlgeschlagen."
    }
}
finally {
    Pop-Location
}

$builtFrontend = Join-Path $frontendRoot "dist\frontend\browser"
if (-not (Test-Path $builtFrontend)) {
    throw "Das Angular-Produktionsverzeichnis wurde nicht gefunden: $builtFrontend"
}

Copy-Item (Join-Path $builtFrontend "*") $frontendOutput -Recurse -Force
Write-Host "HimiFlow wurde nach $OutputRoot veröffentlicht."
Write-Host "API:      $apiOutput"
Write-Host "Frontend: $frontendOutput"
