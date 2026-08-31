[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\sbom"),
    [string]$ProductVersion = "0.9.0-rc.1"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repositoryRoot "backend\EinsparungsApp.sln"
$frontendRoot = Join-Path $repositoryRoot "frontend"
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null

Push-Location $repositoryRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "Die lokalen .NET-Werkzeuge konnten nicht wiederhergestellt werden."
    }

    dotnet tool run dotnet-CycloneDX $solutionPath `
        --output $resolvedOutputDirectory `
        --filename backend.cdx.json `
        --output-format Json `
        --exclude-test-projects `
        --exclude-dev `
        --set-name HimiFlow.Backend `
        --set-version $ProductVersion `
        --set-type Application

    if ($LASTEXITCODE -ne 0) {
        throw "Die Backend-SBOM konnte nicht erzeugt werden."
    }
}
finally {
    Pop-Location
}

Push-Location $frontendRoot
try {
    $frontendSbom = npm sbom --sbom-format=cyclonedx --sbom-type=application
    if ($LASTEXITCODE -ne 0) {
        throw "Die Frontend-SBOM konnte nicht erzeugt werden."
    }

    $frontendSbom | Set-Content -LiteralPath (Join-Path $resolvedOutputDirectory "frontend.cdx.json") -Encoding utf8
}
finally {
    Pop-Location
}

Write-Host "SBOM-Dateien wurden erstellt: $resolvedOutputDirectory"
