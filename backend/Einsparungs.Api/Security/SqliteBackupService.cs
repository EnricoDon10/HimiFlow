using Microsoft.Data.Sqlite;

namespace Einsparungs.Api.Security;

public sealed class SqliteBackupService
{
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;
    private readonly ILogger<SqliteBackupService> logger;

    public SqliteBackupService(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<SqliteBackupService> logger)
    {
        this.configuration = configuration;
        this.environment = environment;
        this.logger = logger;
    }

    public string BackupDirectory => ResolveBackupDirectory();

    public bool IsSqliteProvider => string.Equals(
        configuration["Database:Provider"] ?? "SQLite",
        "SQLite",
        StringComparison.OrdinalIgnoreCase);

    public async Task<BackupFile> CreateAsync(CancellationToken cancellationToken = default)
    {
        EnsureSqliteProvider();

        var sourcePath = ResolveDatabasePath();
        var backupDirectory = ResolveBackupDirectory();

        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException($"Die SQLite-Datenbank wurde nicht gefunden: {sourcePath}");
        }

        Directory.CreateDirectory(backupDirectory);

        var fileName = $"einsparungen_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.db";
        var destinationPath = Path.Combine(backupDirectory, fileName);

        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();

        await using var connection = new SqliteConnection(sourceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = $"VACUUM INTO '{EscapeSqliteLiteral(destinationPath)}';";
            await vacuum.ExecuteNonQueryAsync(cancellationToken);
        }

        var fileInfo = new FileInfo(destinationPath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            throw new InvalidOperationException("Das SQLite-Backup wurde nicht erstellt.");
        }

        await connection.CloseAsync();

        var validation = await ValidateAsync(destinationPath, cancellationToken);
        if (!validation.IsValid)
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException(
                $"Das SQLite-Backup ist nicht lesbar und wurde verworfen: {validation.Result}");
        }

        logger.LogInformation("SQLite-Backup erstellt: {BackupFileName} ({SizeBytes} Bytes)", fileName, fileInfo.Length);
        return new BackupFile(fileName, fileInfo.Length, fileInfo.LastWriteTimeUtc, destinationPath);
    }

    public IReadOnlyList<BackupFile> List()
    {
        var directory = ResolveBackupDirectory();
        if (!Directory.Exists(directory))
        {
            return Array.Empty<BackupFile>();
        }

        return Directory.EnumerateFiles(directory, "*.db", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(info => info.Length > 0)
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => new BackupFile(info.Name, info.Length, info.LastWriteTimeUtc, info.FullName))
            .ToArray();
    }

    public async Task<BackupValidationResult> ValidateNamedAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            return new BackupValidationResult(false, "Ungültiger Backup-Dateiname.");
        }

        var path = Path.Combine(ResolveBackupDirectory(), fileName);
        var fullPath = Path.GetFullPath(path);
        var backupDirectory = Path.GetFullPath(ResolveBackupDirectory())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(backupDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return new BackupValidationResult(false, "Ungültiger Backup-Pfad.");
        }

        return await ValidateAsync(fullPath, cancellationToken);
    }

    public async Task<BackupValidationResult> ValidateAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        EnsureSqliteProvider();

        var fullPath = Path.GetFullPath(backupPath);
        if (!File.Exists(fullPath))
        {
            return new BackupValidationResult(false, "Datei nicht gefunden.");
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(
                await command.ExecuteScalarAsync(cancellationToken)) ?? "Kein Ergebnis";

            return new BackupValidationResult(
                string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase),
                result);
        }
        catch (SqliteException exception)
        {
            logger.LogWarning(exception, "SQLite-Backupprüfung fehlgeschlagen: {BackupPath}", fullPath);
            return new BackupValidationResult(false, "SQLite-Datei ist nicht lesbar.");
        }
    }

    public int PruneExpired(int retentionDays, int minimumBackupsToKeep)
    {
        if (retentionDays <= 0)
        {
            return 0;
        }

        var backupDirectory = ResolveBackupDirectory();
        if (!Directory.Exists(backupDirectory))
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var candidates = Directory
            .EnumerateFiles(backupDirectory, "einsparungen_*.db", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Length > 0)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(Math.Max(1, minimumBackupsToKeep))
            .Where(file => file.LastWriteTimeUtc < cutoff)
            .ToArray();

        foreach (var candidate in candidates)
        {
            candidate.Delete();
        }

        if (candidates.Length > 0)
        {
            logger.LogInformation(
                "{Count} abgelaufene SQLite-Backups wurden nach der Aufbewahrungsregel entfernt.",
                candidates.Length);
        }

        return candidates.Length;
    }

    public string ResolveDatabasePath()
    {
        EnsureSqliteProvider();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection ist nicht konfiguriert.");
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (string.IsNullOrWhiteSpace(dataSource) || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Für lokale Backups wird eine dateibasierte SQLite-Datenbank benötigt.");
        }

        return Path.GetFullPath(dataSource, environment.ContentRootPath);
    }

    private string ResolveBackupDirectory()
    {
        var configuredDirectory = configuration["Backup:Directory"];
        var directory = string.IsNullOrWhiteSpace(configuredDirectory) ? "backups" : configuredDirectory.Trim();
        return Path.GetFullPath(directory, environment.ContentRootPath);
    }

    private static string EscapeSqliteLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private void EnsureSqliteProvider()
    {
        if (!IsSqliteProvider)
        {
            throw new InvalidOperationException(
                "Die lokale Backup-Funktion ist ausschließlich für den SQLite-Betrieb vorgesehen.");
        }
    }

    public sealed record BackupFile(string FileName, long SizeBytes, DateTime CreatedAtUtc, string FullPath);
    public sealed record BackupValidationResult(bool IsValid, string Result);
}
