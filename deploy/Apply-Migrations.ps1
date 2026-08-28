[CmdletBinding()]
param(
    [string]$PublishRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\api")
)

$ErrorActionPreference = "Stop"
$apiAssembly = Join-Path $PublishRoot "Einsparungs.Api.dll"

if (-not (Test-Path $apiAssembly)) {
    throw "Die veröffentlichte API wurde nicht gefunden: $apiAssembly"
}

Push-Location $PublishRoot
try {
    dotnet .\Einsparungs.Api.dll --migrate
    if ($LASTEXITCODE -ne 0) {
        throw "Die Datenbankmigration ist fehlgeschlagen."
    }
}
finally {
    Pop-Location
}

Write-Host "Datenbankmigration erfolgreich abgeschlossen."
