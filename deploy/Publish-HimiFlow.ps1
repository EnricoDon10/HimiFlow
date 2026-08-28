[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts")
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$resolvedRepositoryRoot = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)
$repositoryPrefix = $resolvedRepositoryRoot + [System.IO.Path]::DirectorySeparatorChar

if (-not $resolvedOutputRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Das Publish-Ziel muss aus Sicherheitsgründen innerhalb des Repositorys liegen: $resolvedOutputRoot"
}

$OutputRoot = $resolvedOutputRoot
$backendProject = Join-Path $repositoryRoot "backend\Einsparungs.Api\Einsparungs.Api.csproj"
$frontendRoot = Join-Path $repositoryRoot "frontend"
$apiOutput = Join-Path $OutputRoot "api"
$frontendOutput = Join-Path $OutputRoot "frontend"
$documentationOutput = Join-Path $OutputRoot "documentation"
$sbomOutput = Join-Path $OutputRoot "sbom"
$thirdPartyNotices = Join-Path $repositoryRoot "THIRD-PARTY-NOTICES.md"
$licenseFile = Join-Path $repositoryRoot "LICENSE"
$documentationRoot = Join-Path $repositoryRoot "docs"
$sbomScript = Join-Path $PSScriptRoot "Generate-Sbom.ps1"

foreach ($generatedDirectory in @($apiOutput, $frontendOutput, $documentationOutput, $sbomOutput)) {
    $resolvedGeneratedDirectory = [System.IO.Path]::GetFullPath($generatedDirectory)
    $outputPrefix = $resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar

    if (-not $resolvedGeneratedDirectory.StartsWith($outputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Ungültiges generiertes Zielverzeichnis: $resolvedGeneratedDirectory"
    }

    if (Test-Path -LiteralPath $resolvedGeneratedDirectory) {
        $existingDatabases = Get-ChildItem -LiteralPath $resolvedGeneratedDirectory -Recurse -File -ErrorAction Stop |
            Where-Object { $_.Extension -in @(".db", ".sqlite", ".sqlite3") }
        if ($existingDatabases.Count -gt 0) {
            throw "Publish abgebrochen: Das Ziel enthält lokale Datenbankdateien. Daten zuerst in ein separates Laufzeit-/Backupverzeichnis verschieben: $($existingDatabases.FullName -join ', ')"
        }

        Remove-Item -LiteralPath $resolvedGeneratedDirectory -Recurse -Force
    }
}

New-Item -ItemType Directory -Force -Path $apiOutput, $frontendOutput, $documentationOutput, $sbomOutput | Out-Null

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

$frontendLicenses = Join-Path $frontendRoot "dist\frontend\3rdpartylicenses.txt"
if (-not (Test-Path -LiteralPath $frontendLicenses -PathType Leaf)) {
    throw "Die Frontend-Drittlizenzdatei wurde nicht erzeugt: $frontendLicenses"
}

Copy-Item -LiteralPath $frontendLicenses -Destination $frontendOutput -Force
Copy-Item -LiteralPath $thirdPartyNotices -Destination $OutputRoot -Force
Copy-Item -LiteralPath $licenseFile -Destination $OutputRoot -Force
Copy-Item (Join-Path $documentationRoot "*") $documentationOutput -Recurse -Force

$publishedDatabases = Get-ChildItem -LiteralPath $OutputRoot -Recurse -File -ErrorAction Stop |
    Where-Object { $_.Extension -in @(".db", ".sqlite", ".sqlite3") }
if ($publishedDatabases.Count -gt 0) {
    throw "Das Release-Paket enthält unerwartet lokale Datenbankdateien: $($publishedDatabases.Name -join ', ')"
}

& $sbomScript -OutputDirectory $sbomOutput
if ($LASTEXITCODE -ne 0) {
    throw "Die SBOM-Erzeugung ist fehlgeschlagen."
}

Write-Host "HimiFlow wurde nach $OutputRoot veröffentlicht."
Write-Host "API:      $apiOutput"
Write-Host "Frontend: $frontendOutput"
Write-Host "Dokumentation: $documentationOutput"
Write-Host "SBOM:     $sbomOutput"
