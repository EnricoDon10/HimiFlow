using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class LicenseServiceHardeningTests
{
    [TestMethod]
    public async Task ActiveUserLimit_CountsActiveNonDeletedUsersAndDeactivationFreesASeat()
    {
        var now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        await using var fixture = await LicenseFixture.CreateAsync(now, maxUsers: 2);
        fixture.Db.Users.AddRange(User("one"), User("two"), User("deleted", isDeleted: true));
        await fixture.Db.SaveChangesAsync();

        var full = await fixture.Service.CheckActiveUserSlotAsync();

        Assert.IsFalse(full.SlotAvailable);
        Assert.AreEqual(2, full.MaxUsers);
        Assert.AreEqual(2, full.ActiveUsers);

        var user = await fixture.Db.Users.SingleAsync(item => item.UserName == "two");
        user.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var freed = await fixture.Service.CheckActiveUserSlotAsync();
        Assert.IsTrue(freed.SlotAvailable);
        Assert.AreEqual(1, freed.ActiveUsers);
    }

    [TestMethod]
    public async Task ClockRollbackBeyondTolerance_ReturnsInvalidAndKeepsMonotonicCheckpoint()
    {
        var checkpoint = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        await using var fixture = await LicenseFixture.CreateAsync(
            checkpoint.AddMinutes(-6),
            maxUsers: 25,
            checkpoint.UtcDateTime);

        var validation = await fixture.Service.ValidateCurrentAsync();

        Assert.AreEqual(LicenseStatuses.Invalid, validation.Status);
        var installation = await fixture.Db.LicenseInstallations.AsNoTracking().SingleAsync();
        Assert.AreEqual(checkpoint.UtcDateTime, installation.LastSuccessfulLicenseValidationUtc);
    }

    [TestMethod]
    public async Task SmallClockDifferenceWithinTolerance_RemainsActiveWithoutRegressingCheckpoint()
    {
        var checkpoint = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        await using var fixture = await LicenseFixture.CreateAsync(
            checkpoint.AddMinutes(-4),
            maxUsers: 25,
            checkpoint.UtcDateTime);

        var validation = await fixture.Service.ValidateCurrentAsync();

        Assert.AreEqual(LicenseStatuses.Active, validation.Status);
        var installation = await fixture.Db.LicenseInstallations.AsNoTracking().SingleAsync();
        Assert.AreEqual(checkpoint.UtcDateTime, installation.LastSuccessfulLicenseValidationUtc);
    }

    private static AppUser User(string name, bool isDeleted = false)
    {
        return new AppUser
        {
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            DisplayName = name,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            IsActive = true,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            MustChangePassword = false
        };
    }

    private sealed class LicenseFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private LicenseFixture(
            SqliteConnection connection,
            AppDbContext db,
            LicenseService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public AppDbContext Db { get; }
        public LicenseService Service { get; }

        public static async Task<LicenseFixture> CreateAsync(
            DateTimeOffset now,
            int maxUsers,
            DateTime? checkpoint = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            using var rsa = RSA.Create(2048);
            var values = new Dictionary<string, string?>
            {
                ["License:EnforcementEnabled"] = "true",
                ["License:PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
                ["License:InstallationId"] = "installation-local",
                ["License:ClockRollbackToleranceMinutes"] = "5",
                ["License:ValidationCheckpointIntervalMinutes"] = "60"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            var licenseKey = CreateLicense(rsa, now.UtcDateTime, maxUsers);
            db.LicenseInstallations.Add(new LicenseInstallation
            {
                Id = 1,
                LicenseKey = licenseKey,
                InstalledAt = now.UtcDateTime.AddDays(-1),
                LastSuccessfulLicenseValidationUtc = checkpoint
            });
            await db.SaveChangesAsync();

            var service = new LicenseService(
                db,
                new OfflineLicenseValidator(configuration),
                configuration,
                new TestHostEnvironment(),
                new FixedTimeProvider(now),
                NullLogger<LicenseService>.Instance);
            return new LicenseFixture(connection, db, service);
        }

        private static string CreateLicense(RSA rsa, DateTime now, int maxUsers)
        {
            var payload = new
            {
                licenseId = "LIC-HARDENING-001",
                customerName = "Testkunde",
                product = "HimiFlow",
                validFrom = now.AddDays(-30),
                validUntil = now.AddDays(30),
                gracePeriodDays = 30,
                maxUsers,
                features = Array.Empty<string>(),
                installationId = "installation-local"
            };
            var payloadSegment = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            var signature = rsa.SignData(
                Encoding.UTF8.GetBytes(payloadSegment),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var signatureSegment = Convert.ToBase64String(signature)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return $"HIMIFLOW-LICENSE-V1.{payloadSegment}.{signatureSegment}";
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HimiFlow.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
