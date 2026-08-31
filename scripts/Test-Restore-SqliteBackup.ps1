[CmdletBinding()]
param(
    [string]$PublishRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\api")
)

$ErrorActionPreference = "Stop"
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($PublishRoot)
$apiAssembly = Join-Path $resolvedPublishRoot "Einsparungs.Api.dll"
$restoreScript = Join-Path (Split-Path $PSScriptRoot -Parent) "deploy\Restore-SqliteBackup.ps1"

if (-not (Test-Path -LiteralPath $apiAssembly -PathType Leaf)) {
    throw "Die veröffentlichte API wurde nicht gefunden: $apiAssembly"
}
if (-not (Test-Path -LiteralPath $restoreScript -PathType Leaf)) {
    throw "Das Restore-Skript wurde nicht gefunden: $restoreScript"
}

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("HimiFlow-restore-script-test-" + [Guid]::NewGuid().ToString("N"))
$databaseDirectory = Join-Path $root "data"
$databasePath = Join-Path $databaseDirectory "einsparungen.db"
$backupDirectory = Join-Path $root "backups"
$keyDirectory = Join-Path $root "keys"
$environmentNames = @(
    "ASPNETCORE_ENVIRONMENT",
    "ConnectionStrings__DefaultConnection",
    "Database__Provider",
    "Database__ApplyMigrationsOnStartup",
    "Database__SeedOnStartup",
    "Database__SeedDemoReferenceData",
    "InitialAdmin__UserName",
    "InitialAdmin__DisplayName",
    "InitialAdmin__TemporaryPassword",
    "Security__RequireHttps",
    "License__EnforcementEnabled",
    "Backup__Directory",
    "Backup__AutomaticEnabled",
    "Logging__LogLevel__Default",
    "Logging__LogLevel__Microsoft.EntityFrameworkCore"
)
$previousEnvironment = @{}
$previousPath = $env:PATH

function Invoke-Api {
    param([string[]]$Arguments)

    Push-Location $resolvedPublishRoot
    try {
        & dotnet .\Einsparungs.Api.dll @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "API-Testkommando ist fehlgeschlagen (ExitCode $LASTEXITCODE): $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-Sql {
    param(
        [string]$Database,
        [string]$Sql,
        [switch]$Scalar
    )

    $connection = [Microsoft.Data.Sqlite.SqliteConnection]::new("Data Source=$Database;Pooling=False")
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        try {
            $command.CommandText = $Sql
            if ($Scalar) {
                return $command.ExecuteScalar()
            }
            [void]$command.ExecuteNonQuery()
        }
        finally {
            $command.Dispose()
        }
    }
    finally {
        $connection.Dispose()
    }
}

try {
    New-Item -ItemType Directory -Path $databaseDirectory, $backupDirectory, $keyDirectory -Force | Out-Null
    foreach ($name in $environmentNames) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
    }

    $env:ASPNETCORE_ENVIRONMENT = "Testing"
    $env:ConnectionStrings__DefaultConnection = "Data Source=$databasePath;Pooling=False"
    $env:Database__Provider = "SQLite"
    $env:Database__ApplyMigrationsOnStartup = "false"
    $env:Database__SeedOnStartup = "false"
    $env:Database__SeedDemoReferenceData = "true"
    $env:InitialAdmin__UserName = "restore-test-admin"
    $env:InitialAdmin__DisplayName = "Restore Test Admin"
    $env:InitialAdmin__TemporaryPassword = "Rst!2026_Q7v#N4pL"
    $env:Security__RequireHttps = "false"
    $env:License__EnforcementEnabled = "false"
    $env:Backup__Directory = $backupDirectory
    $env:Backup__AutomaticEnabled = "false"
    $env:Logging__LogLevel__Default = "Warning"
    Set-Item -Path Env:Logging__LogLevel__Microsoft.EntityFrameworkCore -Value "Warning"

    $nativeSqliteDirectory = Join-Path $resolvedPublishRoot "runtimes\win-x64\native"
    if (Test-Path -LiteralPath $nativeSqliteDirectory -PathType Container) {
        $env:PATH = "$nativeSqliteDirectory;$env:PATH"
    }
    Add-Type -Path (Join-Path $resolvedPublishRoot "Microsoft.Data.Sqlite.dll")
    Add-Type -Path (Join-Path $resolvedPublishRoot "SQLitePCLRaw.core.dll")
    Add-Type -Path (Join-Path $resolvedPublishRoot "SQLitePCLRaw.provider.e_sqlite3.dll")
    Add-Type -Path (Join-Path $resolvedPublishRoot "SQLitePCLRaw.batteries_v2.dll")
    [SQLitePCL.Batteries_V2]::Init()

    # Create a real migrated application database and a deterministic probe row.
    Invoke-Api @("--migrate", "--seed")
    Invoke-Sql $databasePath @"
CREATE TABLE RestoreProbe (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);
INSERT INTO RestoreProbe (Id, Value) VALUES (1, 'original');
"@

    # Let the application create the backup that the real restore script will consume.
    $env:Database__SeedDemoReferenceData = "false"
    Invoke-Api @("--backup-now")
    $backup = Get-ChildItem -LiteralPath $backupDirectory -Filter "*.db" -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $backup) {
        throw "Der Test konnte kein SQLite-Backup finden."
    }

    Invoke-Sql $databasePath "UPDATE RestoreProbe SET Value = 'changed' WHERE Id = 1;"
    & pwsh -NoProfile -File $restoreScript -BackupFile $backup.FullName -PublishRoot $resolvedPublishRoot -DatabaseFile $databasePath
    if ($LASTEXITCODE -ne 0) {
        throw "Das normale Restore-Szenario ist fehlgeschlagen."
    }
    if ((Invoke-Sql $databasePath "SELECT Value FROM RestoreProbe WHERE Id = 1;" -Scalar) -ne "original") {
        throw "Das normale Restore hat den ursprünglichen Testwert nicht wiederhergestellt."
    }
    if ((Invoke-Sql $databasePath "PRAGMA integrity_check;" -Scalar) -ne "ok") {
        throw "Die Datenbankintegrität nach dem normalen Restore ist ungültig."
    }

    # The script must also recreate a completely missing target database.
    Remove-Item -LiteralPath $databasePath -Force
    & pwsh -NoProfile -File $restoreScript -BackupFile $backup.FullName -PublishRoot $resolvedPublishRoot -DatabaseFile $databasePath
    if ($LASTEXITCODE -ne 0) {
        throw "Das Restore-Szenario mit fehlender Zieldatenbank ist fehlgeschlagen."
    }
    if ((Invoke-Sql $databasePath "SELECT Value FROM RestoreProbe WHERE Id = 1;" -Scalar) -ne "original") {
        throw "Das Restore bei fehlender Zieldatenbank enthält nicht die ursprünglichen Daten."
    }

    # A corrupt source must fail without damaging the existing target.
    Invoke-Sql $databasePath "UPDATE RestoreProbe SET Value = 'keep-existing' WHERE Id = 1;"
    Set-Content -LiteralPath $backup.FullName -Value "not a sqlite database" -NoNewline -Encoding UTF8
    $corruptOutput = Join-Path $root "corrupt-restore.out.log"
    $corruptError = Join-Path $root "corrupt-restore.err.log"
    $restoreArguments = @(
        "-NoProfile", "-File", $restoreScript,
        "-BackupFile", $backup.FullName,
        "-PublishRoot", $resolvedPublishRoot,
        "-DatabaseFile", $databasePath
    )
    $restoreProcess = Start-Process -FilePath "pwsh" -ArgumentList $restoreArguments -Wait -PassThru -NoNewWindow -RedirectStandardOutput $corruptOutput -RedirectStandardError $corruptError
    $corruptExitCode = $restoreProcess.ExitCode
    if ($corruptExitCode -eq 0) {
        throw "Ein beschädigtes Backup wurde fälschlich erfolgreich wiederhergestellt."
    }
    if ((Invoke-Sql $databasePath "SELECT Value FROM RestoreProbe WHERE Id = 1;" -Scalar) -ne "keep-existing") {
        throw "Das beschädigte Backup hat die bestehende Zieldatenbank verändert."
    }
    if ((Invoke-Sql $databasePath "PRAGMA integrity_check;" -Scalar) -ne "ok") {
        throw "Die bestehende Datenbank ist nach dem fehlgeschlagenen Restore nicht mehr integer."
    }
    if (-not (Get-ChildItem -LiteralPath (Join-Path $databaseDirectory "restore-safety-backups") -Filter "*.db" -File -ErrorAction SilentlyContinue)) {
        throw "Das Restore-Skript hat vor dem fehlgeschlagenen Restore kein Sicherheitsbackup angelegt."
    }

    Write-Host "Restore-Skript-Integrationstest erfolgreich: normales Restore, fehlende Zieldatenbank und beschädigtes Backup geprüft."
}
finally {
    $env:PATH = $previousPath
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name])
    }
    if (Test-Path -LiteralPath $root -PathType Container) {
        [System.IO.Directory]::Delete($root, $true)
    }
}
