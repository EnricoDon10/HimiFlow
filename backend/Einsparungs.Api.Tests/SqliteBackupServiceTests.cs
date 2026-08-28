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
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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
