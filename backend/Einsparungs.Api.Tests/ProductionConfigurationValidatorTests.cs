using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class ProductionConfigurationValidatorTests
{
    [TestMethod]
    public void ProductionRejectsDatabaseUnderWebRoot()
    {
        var root = CreateRoot();
        try
        {
            var options = CreateOptions(root, databasePath: Path.Combine(root, "wwwroot", "data.db"));
            var result = Validate(root, options);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.FailureMessage, "SQLite-Datenbank darf nicht unterhalb von wwwroot liegen");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public void ProductionRejectsBackupDirectoryUnderWebRoot()
    {
        var root = CreateRoot();
        try
        {
            var options = CreateOptions(root, backupDirectory: Path.Combine(root, "wwwroot", "backups"));
            var result = Validate(root, options);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.FailureMessage, "Backup-Verzeichnis darf nicht unterhalb von wwwroot liegen");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public void ProductionRequiresInstallationIdAndPublicKeyWhenEnforced()
    {
        var root = CreateRoot();
        try
        {
            var options = CreateOptions(root);
            options.LicenseInstallationId = null;
            options.LicensePublicKeyPem = null;
            var result = Validate(root, options);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.FailureMessage, "License:InstallationId");
            StringAssert.Contains(result.FailureMessage, "License:PublicKeyPem");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public void ProductionRejectsDemoSeeding()
    {
        var root = CreateRoot();
        try
        {
            var options = CreateOptions(root);
            options.SeedDemoReferenceData = true;
            var result = Validate(root, options);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.FailureMessage, "Demo-/Seed-Daten");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public void ProductionAcceptsCompleteLocalSqliteConfiguration()
    {
        var root = CreateRoot();
        try
        {
            var result = Validate(root, CreateOptions(root));
            Assert.IsTrue(result.Succeeded, result.FailureMessage);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static ProductionConfigurationOptions CreateOptions(
        string root,
        string? databasePath = null,
        string? backupDirectory = null)
    {
        return new ProductionConfigurationOptions
        {
            IsProduction = true,
            Provider = "SQLite",
            ConnectionString = $"Data Source={databasePath ?? Path.Combine(root, "data", "einsparungen.db")}",
            DatabasePath = databasePath ?? Path.Combine(root, "data", "einsparungen.db"),
            BackupDirectory = backupDirectory ?? Path.Combine(root, "backups"),
            DataProtectionKeyRingPath = Path.Combine(root, "keys"),
            LicenseEnforcementEnabled = true,
            LicenseInstallationId = "test-installation",
            LicensePublicKeyPem = "-----BEGIN PUBLIC KEY-----\ntest\n-----END PUBLIC KEY-----",
            RequireHttps = true,
            BackupIntervalHours = 24,
            BackupMaximumAgeHours = 36,
            BackupRetentionDays = 30,
            BackupMinimumBackupsToKeep = 7
        };
    }

    private static ValidateOptionsResult Validate(string root, ProductionConfigurationOptions options)
    {
        var environment = new TestEnvironment(root);
        return new ProductionConfigurationValidator(environment).Validate(Options.DefaultName, options);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-production-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment, IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "HimiFlow.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
