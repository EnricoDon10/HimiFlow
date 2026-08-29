using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class ProviderMigrationSeparationTests
{
    [TestMethod]
    public void Providers_DiscoverOnlyTheirOwnMigrationHistory()
    {
        var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var sqlite = new AppDbContext(sqliteOptions);
        using var sqlServer = new SqlServerAppDbContextFactory().CreateDbContext([]);

        var sqliteMigrations = sqlite.Database.GetMigrations().ToArray();
        var sqlServerMigrations = sqlServer.Database.GetMigrations().ToArray();

        Assert.IsTrue(sqliteMigrations.Any(item => item.EndsWith("_ProductionQueryIndexes", StringComparison.Ordinal)));
        Assert.IsFalse(sqliteMigrations.Any(item => item.EndsWith("_SqlServerInitial", StringComparison.Ordinal)));
        Assert.AreEqual(3, sqlServerMigrations.Length);
        Assert.IsTrue(sqlServerMigrations.Any(item => item.EndsWith("_SqlServerInitial", StringComparison.Ordinal)));
        Assert.IsTrue(sqlServerMigrations.Any(item => item.EndsWith("_LicenseEnforcementHardening", StringComparison.Ordinal)));
        Assert.IsTrue(sqlServerMigrations.Any(item => item.EndsWith("_AuditRetentionSupport", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SqlServerModel_UsesProductionTypesConstraintsAndFixedLicenseKey()
    {
        using var sqlServer = new SqlServerAppDbContextFactory().CreateDbContext([]);
        var designTimeModel = sqlServer.GetService<IDesignTimeModel>().Model;
        var savings = designTimeModel.FindEntityType(typeof(SavingsEntry));
        var license = designTimeModel.FindEntityType(typeof(LicenseInstallation));

        Assert.IsNotNull(savings);
        Assert.AreEqual("decimal(18,2)", savings.FindProperty(nameof(SavingsEntry.SavingAmount))?.GetColumnType());
        Assert.IsTrue(savings.GetCheckConstraints().Any(item =>
            item.Name == "CK_SavingsEntry_Kvnr_Length" && item.Sql.Contains("LEN", StringComparison.Ordinal)));
        Assert.IsNotNull(license);
        Assert.AreEqual(
            ValueGenerated.Never,
            license.FindProperty(nameof(LicenseInstallation.Id))?.ValueGenerated);
    }
}
