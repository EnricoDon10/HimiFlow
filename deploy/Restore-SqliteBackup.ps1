[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [string]$PublishRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\api"),

    [Parameter(Mandatory = $true)]
    [string]$DatabaseFile
)

$ErrorActionPreference = "Stop"
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($PublishRoot)
$resolvedBackupFile = [System.IO.Path]::GetFullPath($BackupFile)
$resolvedDatabaseFile = [System.IO.Path]::GetFullPath($DatabaseFile)
$databaseDirectory = [System.IO.Path]::GetDirectoryName($resolvedDatabaseFile)
$apiAssembly = Join-Path $resolvedPublishRoot "Einsparungs.Api.dll"
$temporaryRestoreFile = $null
$safetyBackupFile = $null
$replaceBackupFile = $null
$hadExistingDatabase = $false
$restoreResult = "Failed"
$integrityResult = "NotRun"

function Test-PathWithin {
    param([string]$Path, [string]$Parent)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    return $fullPath.StartsWith($fullParent, [System.StringComparison]::OrdinalIgnoreCase)
}

function Write-RestoreLog {
    param([string]$Result, [string]$Integrity, [string]$ErrorMessage = $null)

    $logDirectory = Join-Path $databaseDirectory "restore-logs"
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $logFile = Join-Path $logDirectory ("restore_" + (Get-Date -Format "yyyyMMdd_HHmmss_fff") + "_" + [Guid]::NewGuid().ToString("N") + ".json")
    $payload = [ordered]@{
        timestampUtc = [DateTime]::UtcNow.ToString("o")
        backupFile = $resolvedBackupFile
        targetDatabase = $resolvedDatabaseFile
        result = $Result
        integrityCheck = $Integrity
    }
    if ($ErrorMessage) {
        $payload.error = $ErrorMessage
    }
    $payload | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $logFile -Encoding UTF8
}

try {
    if (-not (Test-Path -LiteralPath $apiAssembly -PathType Leaf)) {
        throw "Die veröffentlichte API wurde nicht gefunden: $apiAssembly"
    }

    if (-not (Test-Path -LiteralPath $resolvedBackupFile -PathType Leaf)) {
        throw "Die Backup-Datei wurde nicht gefunden: $resolvedBackupFile"
    }

    if ([System.IO.Path]::GetExtension($resolvedBackupFile) -ne ".db") {
        throw "Es dürfen nur SQLite-Backups mit der Endung .db verwendet werden."
    }

    $webRoot = Join-Path $resolvedPublishRoot "wwwroot"
    if (Test-Path -LiteralPath $webRoot -PathType Container) {
        if (Test-PathWithin $resolvedBackupFile $webRoot -or Test-PathWithin $resolvedDatabaseFile $webRoot) {
            throw "Restore-Quellen und -Ziele im öffentlich erreichbaren WebRoot sind nicht zulässig."
        }
    }

    if ($resolvedBackupFile.Equals($resolvedDatabaseFile, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Backup-Datei und Zieldatenbank dürfen nicht identisch sein."
    }

    New-Item -ItemType Directory -Path $databaseDirectory -Force | Out-Null
    $hadExistingDatabase = Test-Path -LiteralPath $resolvedDatabaseFile -PathType Leaf

    if ($hadExistingDatabase) {
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
    }

    Push-Location $resolvedPublishRoot
    try {
        dotnet .\Einsparungs.Api.dll --validate-backup $resolvedBackupFile
        if ($LASTEXITCODE -ne 0) {
            throw "Das ausgewählte Backup ist ungültig."
        }

        if ($hadExistingDatabase) {
            $safetyDirectory = Join-Path $databaseDirectory "restore-safety-backups"
            New-Item -ItemType Directory -Path $safetyDirectory -Force | Out-Null
            $safetyBackupFile = Join-Path $safetyDirectory ("pre_restore_" + (Get-Date -Format "yyyyMMdd_HHmmss") + "_" + [Guid]::NewGuid().ToString("N") + ".db")
            Copy-Item -LiteralPath $resolvedDatabaseFile -Destination $safetyBackupFile
            dotnet .\Einsparungs.Api.dll --validate-backup $safetyBackupFile
            if ($LASTEXITCODE -ne 0) {
                throw "Das Sicherheitsbackup der exakten Zieldatenbank ist fehlgeschlagen oder ungültig."
            }

            foreach ($sidecar in @("$resolvedDatabaseFile-wal", "$resolvedDatabaseFile-shm")) {
                if (Test-Path -LiteralPath $sidecar -PathType Leaf) {
                    Copy-Item -LiteralPath $sidecar -Destination (Join-Path $safetyDirectory ([System.IO.Path]::GetFileName($safetyBackupFile) + [System.IO.Path]::GetExtension($sidecar)))
                }
            }
        }

        $temporaryRestoreFile = Join-Path $databaseDirectory (".himiflow-restore-" + [Guid]::NewGuid().ToString("N") + ".tmp")
        if (-not (Test-PathWithin $temporaryRestoreFile $databaseDirectory)) {
            throw "Ungültiger temporärer Restore-Pfad."
        }
        Copy-Item -LiteralPath $resolvedBackupFile -Destination $temporaryRestoreFile

        dotnet .\Einsparungs.Api.dll --validate-backup $temporaryRestoreFile
        if ($LASTEXITCODE -ne 0) {
            throw "Das temporäre Restore-Image hat die Integritätsprüfung nicht bestanden."
        }

        foreach ($sidecarPath in @("$resolvedDatabaseFile-wal", "$resolvedDatabaseFile-shm")) {
            if (Test-Path -LiteralPath $sidecarPath -PathType Leaf) {
                Remove-Item -LiteralPath $sidecarPath -Force
            }
        }

        if ($hadExistingDatabase) {
            $replaceBackupFile = $resolvedDatabaseFile + ".swap-" + [Guid]::NewGuid().ToString("N") + ".db"
            [System.IO.File]::Replace($temporaryRestoreFile, $resolvedDatabaseFile, $replaceBackupFile, $true)
            if (Test-Path -LiteralPath $replaceBackupFile -PathType Leaf) {
                Remove-Item -LiteralPath $replaceBackupFile -Force
            }
        }
        else {
            [System.IO.File]::Move($temporaryRestoreFile, $resolvedDatabaseFile)
        }
        $temporaryRestoreFile = $null

        dotnet .\Einsparungs.Api.dll --validate-backup $resolvedDatabaseFile
        if ($LASTEXITCODE -ne 0) {
            throw "Die wiederhergestellte Datenbank hat die Integritätsprüfung nicht bestanden."
        }
        $integrityResult = "ok"
        $restoreResult = "Succeeded"
    }
    finally {
        Pop-Location
    }

    Write-RestoreLog $restoreResult $integrityResult
    Write-Host "SQLite-Restore erfolgreich abgeschlossen: $resolvedDatabaseFile"
    if ($safetyBackupFile) {
        Write-Host "Sicherheitsbackup der bisherigen Zieldatenbank: $safetyBackupFile"
    }
    exit 0
}
catch {
    $errorMessage = $_.Exception.Message
    if ($temporaryRestoreFile -and (Test-Path -LiteralPath $temporaryRestoreFile -PathType Leaf)) {
        Remove-Item -LiteralPath $temporaryRestoreFile -Force -ErrorAction SilentlyContinue
    }
    if ($hadExistingDatabase -and $safetyBackupFile -and (Test-Path -LiteralPath $safetyBackupFile -PathType Leaf)) {
        try {
            $rollbackFile = Join-Path $databaseDirectory (".himiflow-rollback-" + [Guid]::NewGuid().ToString("N") + ".tmp")
            Copy-Item -LiteralPath $safetyBackupFile -Destination $rollbackFile
            if (Test-Path -LiteralPath $resolvedDatabaseFile -PathType Leaf) {
                $rollbackSwap = $resolvedDatabaseFile + ".rollback-" + [Guid]::NewGuid().ToString("N") + ".db"
                [System.IO.File]::Replace($rollbackFile, $resolvedDatabaseFile, $rollbackSwap, $true)
                if (Test-Path -LiteralPath $rollbackSwap -PathType Leaf) {
                    Remove-Item -LiteralPath $rollbackSwap -Force
                }
            }
            else {
                [System.IO.File]::Move($rollbackFile, $resolvedDatabaseFile)
            }

            $safetyDirectory = Split-Path $safetyBackupFile -Parent
            foreach ($sidecarSuffix in @("-wal", "-shm")) {
                $safetySidecar = Join-Path $safetyDirectory ([System.IO.Path]::GetFileName($safetyBackupFile) + ".db" + $sidecarSuffix)
                if (Test-Path -LiteralPath $safetySidecar -PathType Leaf) {
                    Copy-Item -LiteralPath $safetySidecar -Destination ($resolvedDatabaseFile + $sidecarSuffix) -Force
                }
            }
            $errorMessage = "Restore fehlgeschlagen; die vorherige Datenbank wurde aus dem Sicherheitsbackup wiederhergestellt. $errorMessage"
        }
        catch {
            $errorMessage = "Restore fehlgeschlagen und automatischer Rollback war nicht möglich. Sicherheitsbackup erhalten. Ursache: $errorMessage; Rollback: $($_.Exception.Message)"
        }
    }
    elseif (-not $hadExistingDatabase -and (Test-Path -LiteralPath $resolvedDatabaseFile -PathType Leaf)) {
        Remove-Item -LiteralPath $resolvedDatabaseFile -Force -ErrorAction SilentlyContinue
        foreach ($sidecarSuffix in @("-wal", "-shm")) {
            $sidecar = $resolvedDatabaseFile + $sidecarSuffix
            if (Test-Path -LiteralPath $sidecar -PathType Leaf) {
                Remove-Item -LiteralPath $sidecar -Force -ErrorAction SilentlyContinue
            }
        }
    }
    try {
        Write-RestoreLog "Failed" $integrityResult $errorMessage
    }
    catch {
        Write-Warning "Recovery-Log konnte nicht geschrieben werden: $($_.Exception.Message)"
    }
    Write-Error $errorMessage
    exit 1
}
