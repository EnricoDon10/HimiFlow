using Einsparungs.Api.Controllers;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class BackupStatusEvaluatorTests
{
    [TestMethod]
    public void Evaluate_UsesAbsoluteDirectoryAndReportsMissingBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-status-test-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(root, "external", "backups");
        Directory.CreateDirectory(root);

        try
        {
            var (service, evaluator) = CreateEvaluator(root, backupDirectory);

            var status = evaluator.Evaluate();

            Assert.AreEqual(Path.GetFullPath(backupDirectory), service.BackupDirectory);
            Assert.AreEqual("MISSING", status.Status);
            Assert.IsTrue(status.IsMissing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Evaluate_ReportsBackupOlderThanMaximumAgeAsOverdue()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-status-test-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(root, "backups");
        Directory.CreateDirectory(backupDirectory);

        try
        {
            var backupPath = Path.Combine(backupDirectory, "einsparungen_old.db");
            File.WriteAllBytes(backupPath, "SQLite format 3\0"u8.ToArray());
            File.SetLastWriteTimeUtc(backupPath, new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc));
            var (_, evaluator) = CreateEvaluator(root, backupDirectory);

            var status = evaluator.Evaluate();

            Assert.AreEqual("OVERDUE", status.Status);
            Assert.IsTrue(status.IsOverdue);
            Assert.AreEqual(36, status.MaximumAgeHours);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Evaluate_EmptyExistingDirectoryIsAValidMissingState()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-status-test-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(root, "backups");
        Directory.CreateDirectory(backupDirectory);

        try
        {
            var (_, evaluator) = CreateEvaluator(root, backupDirectory);
            var status = evaluator.Evaluate();

            Assert.AreEqual("MISSING", status.Status);
            Assert.AreEqual(0, status.AvailableBackups);
            Assert.IsTrue(status.IsMissing);
            Assert.IsFalse(status.IsOverdue);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Evaluate_IgnoresUnreadableNonSqliteFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-status-test-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(root, "backups");
        Directory.CreateDirectory(backupDirectory);

        try
        {
            File.WriteAllText(Path.Combine(backupDirectory, "einsparungen_corrupt.db"), "not a sqlite database");
            var (_, evaluator) = CreateEvaluator(root, backupDirectory);
            var status = evaluator.Evaluate();

            Assert.AreEqual("MISSING", status.Status);
            Assert.AreEqual(0, status.AvailableBackups);
            Assert.IsTrue(status.IsMissing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Evaluate_DisabledAutomaticBackupsStillReturnsAStatusObject()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-status-test-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(root, "backups");
        Directory.CreateDirectory(backupDirectory);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(root, "source.db")}",
                    ["Database:Provider"] = "SQLite",
                    ["Backup:Directory"] = backupDirectory,
                    ["Backup:AutomaticEnabled"] = "false",
                    ["Backup:IntervalHours"] = "24",
                    ["Backup:MaximumAgeHours"] = "36"
                })
                .Build();
            var service = new SqliteBackupService(configuration, new TestHostEnvironment(root), NullLogger<SqliteBackupService>.Instance);
            var evaluator = new BackupStatusEvaluator(service, configuration, new FixedTimeProvider(new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero)));

            var status = evaluator.Evaluate();

            Assert.AreEqual("DISABLED", status.Status);
            Assert.IsFalse(status.AutomaticEnabled);
            Assert.AreEqual(0, status.AvailableBackups);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void BackupStatusEndpoint_ReturnsProblemDetailsForInvalidDirectoryConfiguration()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-status-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(root, "source.db")}",
                    ["Database:Provider"] = "SQLite",
                    ["Backup:Directory"] = "\0",
                    ["Backup:AutomaticEnabled"] = "true"
                })
                .Build();
            var service = new SqliteBackupService(configuration, new TestHostEnvironment(root), NullLogger<SqliteBackupService>.Instance);
            var evaluator = new BackupStatusEvaluator(service, configuration, TimeProvider.System);
            var controller = new OperationsController(service, null!, evaluator)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = controller.GetBackupStatus().Result as ObjectResult;
            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            var problem = result.Value as ProblemDetails;
            Assert.IsNotNull(problem);
            StringAssert.Contains(problem.Detail!, "Backup");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (SqliteBackupService Service, BackupStatusEvaluator Evaluator) CreateEvaluator(
        string root,
        string backupDirectory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(root, "source.db")}",
                ["Database:Provider"] = "SQLite",
                ["Backup:Directory"] = backupDirectory,
                ["Backup:AutomaticEnabled"] = "true",
                ["Backup:IntervalHours"] = "24",
                ["Backup:MaximumAgeHours"] = "36"
            })
            .Build();
        var service = new SqliteBackupService(
            configuration,
            new TestHostEnvironment(root),
            NullLogger<SqliteBackupService>.Instance);
        var evaluator = new BackupStatusEvaluator(
            service,
            configuration,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero)));
        return (service, evaluator);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HimiFlow.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
