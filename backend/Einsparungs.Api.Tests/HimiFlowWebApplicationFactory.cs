using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Einsparungs.Api.Tests;

internal sealed class HimiFlowWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string InitialAdminUserName = "integration.admin";
    public const string InitialAdminPassword = "T9!vK2@pL7#xR4$q";
    public const string ChangedAdminPassword = "Z8@wM3#qN6!sP2%y";

    private readonly string rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"HimiFlow-http-tests-{Guid.NewGuid():N}");
    private readonly IReadOnlyDictionary<string, string?> configurationOverrides;

    public HimiFlowWebApplicationFactory(IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        this.configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        Directory.CreateDirectory(rootDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            $"Data Source={Path.Combine(rootDirectory, "integration.db")};Pooling=False");
        builder.UseSetting("Database:Provider", "SQLite");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(rootDirectory, "integration.db")};Pooling=False",
                ["Database:Provider"] = "SQLite",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Database:SeedOnStartup"] = "true",
                ["InitialAdmin:UserName"] = InitialAdminUserName,
                ["InitialAdmin:DisplayName"] = "Integration SystemAdmin",
                ["InitialAdmin:TemporaryPassword"] = InitialAdminPassword,
                ["Security:RequireHttps"] = "false",
                ["License:EnforcementEnabled"] = "false",
                ["Backup:AutomaticEnabled"] = "false",
                ["Backup:Directory"] = Path.Combine(rootDirectory, "backups"),
                ["Audit:CleanupEnabled"] = "false",
                ["Audit:RetentionDays"] = "0"
            });
            configuration.AddInMemoryCollection(configurationOverrides);
        });
    }

    public async Task<AppUser> AddUserAsync(
        string userName,
        string password,
        string roleName,
        bool mustChangePassword = false)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var team = roleName == ApplicationRoles.SystemAdmin
            ? null
            : await db.Teams.OrderBy(team => team.Id).FirstAsync();
        var role = await db.Roles.SingleAsync(role => role.Name == roleName);
        var user = new AppUser
        {
            UserName = userName,
            DisplayName = $"Integration {roleName}",
            TeamId = team?.Id,
            MustChangePassword = mustChangePassword,
            PasswordChangedAt = mustChangePassword ? null : DateTime.UtcNow,
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        db.UserRoles.Add(new AppUserRole { AppUserId = user.Id, AppRoleId = role.Id });
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<SavingsEntry> AddSavingsAsync(AppUser owner, string kvnr = "A123456789")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teamId = owner.TeamId ?? throw new InvalidOperationException("Testbenutzer benötigt ein Team.");
        var entry = new SavingsEntry
        {
            Month = new DateTime(2026, 8, 1),
            Kvnr = kvnr,
            OldKvAmount = 100m,
            NewKvAmount = 40m,
            SavingAmount = 60m,
            TeamId = teamId,
            SavingReasonId = await db.SavingReasons.Select(item => item.Id).FirstAsync(),
            ProductGroupId = await db.ProductGroups.Select(item => item.Id).FirstAsync(),
            CreatedByUserId = owner.Id,
            TransmissionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };
        db.SavingsEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async Task SetUserActiveAsync(Guid userId, bool isActive)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Testbenutzer wurde nicht gefunden.");
        user.IsActive = isActive;
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
    }

    public async Task<bool> CheckPasswordAsync(string userName, string password)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException("Testbenutzer wurde nicht gefunden.");
        return await userManager.CheckPasswordAsync(user, password);
    }

    public async Task<SignInResult> CheckSignInAsync(string userName, string password)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException("Testbenutzer wurde nicht gefunden.");
        return await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
    }

    public async Task<(int FailedCount, DateTimeOffset? LockoutEnd, bool IsLockedOut)> GetLockoutStateAsync(string userName)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException("Testbenutzer wurde nicht gefunden.");
        return (user.AccessFailedCount, user.LockoutEnd, await userManager.IsLockedOutAsync(user));
    }

    public string GetConfiguredConnectionString()
    {
        return Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection") ?? "missing";
    }

    public async Task SetInstalledLicenseAsync(string licenseKey)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var installation = await db.LicenseInstallations.SingleOrDefaultAsync(item => item.Id == 1);
        if (installation is null)
        {
            installation = new LicenseInstallation { Id = 1 };
            db.LicenseInstallations.Add(installation);
        }

        installation.LicenseKey = licenseKey;
        installation.InstalledAt = DateTime.UtcNow;
        installation.LastSuccessfulLicenseValidationUtc = null;
        await db.SaveChangesAsync();
    }

    public async Task<int> GetFirstTeamIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Teams.OrderBy(team => team.Id).Select(team => team.Id).FirstAsync();
    }

    public async Task<int> CreateTeamAsync(string displayName)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var team = new Team
        {
            Code = $"TEST-{Guid.NewGuid():N}"[..20],
            Name = displayName,
            DisplayName = displayName,
            IsActive = true
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    public async Task SetTeamActiveAsync(int teamId, bool isActive)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var team = await db.Teams.SingleAsync(item => item.Id == teamId);
        team.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
