[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [string]$PublishRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\api"),

    [string]$DatabaseFile
)

$ErrorActionPreference = "Stop"
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($PublishRoot)
$apiAssembly = Join-Path $resolvedPublishRoot "Einsparungs.Api.dll"
$resolvedBackupFile = [System.IO.Path]::GetFullPath($BackupFile)

if ([string]::IsNullOrWhiteSpace($DatabaseFile)) {
    $DatabaseFile = Join-Path $resolvedPublishRoot "einsparungen.db"
}

$resolvedDatabaseFile = [System.IO.Path]::GetFullPath($DatabaseFile)
$databaseDirectory = [System.IO.Path]::GetDirectoryName($resolvedDatabaseFile)

if (-not (Test-Path -LiteralPath $apiAssembly -PathType Leaf)) {
    throw "Die veröffentlichte API wurde nicht gefunden: $apiAssembly"
}

if (-not (Test-Path -LiteralPath $resolvedBackupFile -PathType Leaf)) {
    throw "Die Backup-Datei wurde nicht gefunden: $resolvedBackupFile"
}

if (-not (Test-Path -LiteralPath $resolvedDatabaseFile -PathType Leaf)) {
    throw "Die Zieldatenbank wurde nicht gefunden: $resolvedDatabaseFile"
}

if ($resolvedBackupFile.Equals($resolvedDatabaseFile, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Backup-Datei und Zieldatenbank dürfen nicht identisch sein."
}

try {
    $lockCheck = [System.IO.File]::Open(
        $resolvedDatabaseFile,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    $lockCheck.Dispose()
}
catch {
    throw "Die Datenbank ist noch in Benutzung. HimiFlow muss vor dem Restore vollständig beendet werden."
}

Push-Location $resolvedPublishRoot
try {
    dotnet .\Einsparungs.Api.dll --validate-backup $resolvedBackupFile
    if ($LASTEXITCODE -ne 0) {
        throw "Das ausgewählte Backup ist ungültig."
    }

    dotnet .\Einsparungs.Api.dll --backup-now
    if ($LASTEXITCODE -ne 0) {
        throw "Das Sicherheitsbackup der aktuellen Datenbank ist fehlgeschlagen."
    }

    $temporaryRestoreFile = Join-Path $databaseDirectory (".himiflow-restore-" + [Guid]::NewGuid().ToString("N") + ".tmp")
    $resolvedTemporaryRestoreFile = [System.IO.Path]::GetFullPath($temporaryRestoreFile)
    if (-not $resolvedTemporaryRestoreFile.StartsWith($databaseDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Ungültiger temporärer Restore-Pfad."
    }

    Copy-Item -LiteralPath $resolvedBackupFile -Destination $resolvedTemporaryRestoreFile

    foreach ($sidecarPath in @("$resolvedDatabaseFile-wal", "$resolvedDatabaseFile-shm")) {
        if (Test-Path -LiteralPath $sidecarPath -PathType Leaf) {
            Remove-Item -LiteralPath $sidecarPath -Force
        }
    }

    Move-Item -LiteralPath $resolvedTemporaryRestoreFile -Destination $resolvedDatabaseFile -Force

    dotnet .\Einsparungs.Api.dll --validate-backup $resolvedDatabaseFile
    if ($LASTEXITCODE -ne 0) {
        throw "Die wiederhergestellte Datenbank hat die Integritätsprüfung nicht bestanden."
    }
}
finally {
    Pop-Location
}

Write-Host "SQLite-Restore erfolgreich abgeschlossen: $resolvedDatabaseFile"
Write-Host "Vor dem Restore wurde automatisch ein Sicherheitsbackup der bisherigen Datenbank erstellt."
