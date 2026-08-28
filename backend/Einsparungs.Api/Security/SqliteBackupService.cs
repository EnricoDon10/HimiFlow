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

    public async Task<BackupFile> CreateAsync(CancellationToken cancellationToken = default)
    {
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
        logger.LogInformation("SQLite-Backup erstellt: {BackupFileName} ({SizeBytes} Bytes)", fileName, fileInfo.Length);
        return new BackupFile(fileName, fileInfo.Length, fileInfo.CreationTimeUtc, destinationPath);
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
            .OrderByDescending(info => info.CreationTimeUtc)
            .Select(info => new BackupFile(info.Name, info.Length, info.CreationTimeUtc, info.FullName))
            .ToArray();
    }

    public string ResolveDatabasePath()
    {
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

    public sealed record BackupFile(string FileName, long SizeBytes, DateTime CreatedAtUtc, string FullPath);
}
