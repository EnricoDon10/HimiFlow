using Einsparungs.Api.Security;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class SqliteBackupServiceTests
{
    [TestMethod]
    public async Task CreateAsync_CreatesReadableSnapshotAndListsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var databasePath = Path.Combine(root, "einsparungen.db");
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Sample (Id INTEGER PRIMARY KEY, Value TEXT); INSERT INTO Sample (Value) VALUES ('ok');";
                await command.ExecuteNonQueryAsync();
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}",
                    ["Backup:Directory"] = "backups"
                })
                .Build();
            var environment = new TestHostEnvironment(root);
            var service = new SqliteBackupService(
                configuration,
                environment,
                NullLogger<SqliteBackupService>.Instance);

            var backup = await service.CreateAsync();

            Assert.IsTrue(File.Exists(backup.FullPath));
            Assert.IsTrue(backup.SizeBytes > 0);
            Assert.AreEqual(1, service.List().Count);
            var validation = await service.ValidateAsync(backup.FullPath);
            Assert.IsTrue(validation.IsValid);
            Assert.AreEqual("ok", validation.Result);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ValidateAsync_RejectsNonSqliteFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var invalidPath = Path.Combine(root, "invalid.db");
            await File.WriteAllTextAsync(invalidPath, "not a sqlite database");
            var service = CreateService(root, Path.Combine(root, "source.db"));

            var validation = await service.ValidateAsync(invalidPath);

            Assert.IsFalse(validation.IsValid);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task BackupCanBeValidatedAndRestoredIntoSeparateDatabaseWithoutTouchingSource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-restore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sourcePath = Path.Combine(root, "source.db");
            await using (var connection = new SqliteConnection($"Data Source={sourcePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Savings (Id INTEGER PRIMARY KEY, Amount DECIMAL NOT NULL); INSERT INTO Savings (Amount) VALUES (12.50), (7.50);";
                await command.ExecuteNonQueryAsync();
            }

            var service = CreateService(root, sourcePath);
            var backup = await service.CreateAsync();
            var validation = await service.ValidateAsync(backup.FullPath);
            Assert.IsTrue(validation.IsValid);

            var restoredPath = Path.Combine(root, "restored.db");
            File.Copy(backup.FullPath, restoredPath);
            await using var restored = new SqliteConnection($"Data Source={restoredPath};Mode=ReadOnly;Pooling=False");
            await restored.OpenAsync();
            await using var sumCommand = restored.CreateCommand();
            sumCommand.CommandText = "SELECT COUNT(*), COALESCE(SUM(Amount), 0) FROM Savings;";
            await using var reader = await sumCommand.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual(2L, reader.GetInt64(0));
            Assert.AreEqual(20m, Convert.ToDecimal(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture));
            Assert.IsTrue(File.Exists(sourcePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static SqliteBackupService CreateService(string root, string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}",
                ["Database:Provider"] = "SQLite",
                ["Backup:Directory"] = "backups"
            })
            .Build();

        return new SqliteBackupService(
            configuration,
            new TestHostEnvironment(root),
            NullLogger<SqliteBackupService>.Instance);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HimiFlow.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
