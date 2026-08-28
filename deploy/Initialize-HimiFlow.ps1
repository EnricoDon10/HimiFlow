[CmdletBinding()]
param(
    [string]$PublishRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\api")
)

$ErrorActionPreference = "Stop"
$apiAssembly = Join-Path $PublishRoot "Einsparungs.Api.dll"

if (-not (Test-Path $apiAssembly)) {
    throw "Die veröffentlichte API wurde nicht gefunden: $apiAssembly"
}

if ([string]::IsNullOrWhiteSpace($env:InitialAdmin__TemporaryPassword)) {
    throw "InitialAdmin__TemporaryPassword muss als Umgebungsvariable gesetzt werden."
}

Push-Location $PublishRoot
try {
    dotnet .\Einsparungs.Api.dll --migrate --seed
    if ($LASTEXITCODE -ne 0) {
        throw "Das initiale Datenbank-Setup ist fehlgeschlagen."
    }
}
finally {
    Pop-Location
}

Write-Host "Initiales HimiFlow-Setup erfolgreich abgeschlossen."
