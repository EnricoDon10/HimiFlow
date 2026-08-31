using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<AppUser> userManager,
        IConfiguration configuration,
        bool applyMigrations = true,
        bool seedReferenceData = true)
    {
        if (applyMigrations)
        {
            await DatabasePreflight.ValidateBeforeMigrationAsync(db);
            await db.Database.MigrateAsync();
        }

        // Rollen sind technische Systemdaten und werden immer sichergestellt.
        // Teams, Gründe und Produktgruppen sind kundenspezifische Demo-Stammdaten
        // und werden nur über die ausdrücklich aktivierte Development-Konfiguration
        // eingespielt.
        await SeedRolesAsync(db);

        if (seedReferenceData)
        {
            await SeedDemoReferenceDataAsync(db);
        }

        await EnsureInitialSystemAdminAsync(db, userManager, configuration);
    }

    public static async Task SeedReferenceDataAsync(AppDbContext db)
    {
        await SeedRolesAsync(db);
        await SeedDemoReferenceDataAsync(db);
    }

    public static async Task SeedDemoReferenceDataAsync(AppDbContext db)
    {
        await SeedTeamsAsync(db);
        await SeedSavingReasonsAsync(db);
        await SeedProductGroupsAsync(db);
    }

    private static async Task SeedRolesAsync(AppDbContext db)
    {
        var existingRoleNames = await db.Roles
            .Select(role => role.Name)
            .ToListAsync();

        var missingRoles = ApplicationRoles.All
            .Where(roleName => !existingRoleNames.Contains(roleName, StringComparer.Ordinal))
            .Select(roleName => new AppRole { Name = roleName })
            .ToList();

        if (missingRoles.Count == 0)
        {
            return;
        }

        db.Roles.AddRange(missingRoles);
        await db.SaveChangesAsync();
    }

    private static async Task SeedTeamsAsync(AppDbContext db)
    {
        if (await db.Teams.AnyAsync())
        {
            return;
        }

        db.Teams.AddRange(
            new Team { Code = "3410", Name = "Bochum 1", DisplayName = "Bochum 1 (3410)" },
            new Team { Code = "3420", Name = "Bochum 2", DisplayName = "Bochum 2 (3420)" },
            new Team { Code = "3430", Name = "Bochum 3", DisplayName = "Bochum 3 (3430)" },
            new Team { Code = "3440", Name = "Rüsselsheim", DisplayName = "Rüsselsheim (3440)" },
            new Team { Code = "3450", Name = "Lübeck", DisplayName = "Lübeck (3450)" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedSavingReasonsAsync(AppDbContext db)
    {
        if (await db.SavingReasons.AnyAsync())
        {
            return;
        }

        db.SavingReasons.AddRange(
            new SavingReason { Name = "vollständig keine med. Notwendigkeit" },
            new SavingReason { Name = "teilweise keine med. Notwendigkeit" },
            new SavingReason { Name = "Lagerversorgung" },
            new SavingReason { Name = "Kürzung auf Vertragspreis" },
            new SavingReason { Name = "Kürzung allgemein" },
            new SavingReason { Name = "Rabatt" },
            new SavingReason { Name = "Umversorgung auf anderes Himi" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedProductGroupsAsync(AppDbContext db)
    {
        if (await db.ProductGroups.AnyAsync())
        {
            return;
        }

        db.ProductGroups.AddRange(
            new ProductGroup { DisplayValue = "18.50.03.0xxx, Aktivrollstuhl", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "17.10.xx.xxxx, Kompressionsartikel - nicht apparativ ARM", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "31.03.xx.xxxx, Pflegehilfsmittel zur Körperpflege", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "14.24.xx.xxxx, Inhalations- und Atemtherapiegeräte", ImportedBy = "system" },
            new ProductGroup { DisplayValue = "19.40.xx.xxxx, Krankenpflegeartikel", ImportedBy = "system" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task EnsureInitialSystemAdminAsync(
        AppDbContext db,
        UserManager<AppUser> userManager,
        IConfiguration configuration)
    {
        var hasActiveSystemAdmin = await db.Users.AnyAsync(user =>
            user.IsActive &&
            !user.IsDeleted &&
            user.UserRoles.Any(userRole => userRole.AppRole.Name == ApplicationRoles.SystemAdmin));

        if (hasActiveSystemAdmin)
        {
            return;
        }

        if (await db.Users.AnyAsync())
        {
            throw new InvalidOperationException(
                "No active SystemAdmin exists. Restore or assign a SystemAdmin before starting HimiFlow.");
        }

        var temporaryPassword = configuration["InitialAdmin:TemporaryPassword"];

        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            throw new InvalidOperationException(
                "InitialAdmin:TemporaryPassword is required for the first database setup. Configure it with .NET User Secrets.");
        }

        var user = new AppUser
        {
            UserName = configuration["InitialAdmin:UserName"]?.Trim() ?? "admin",
            DisplayName = configuration["InitialAdmin:DisplayName"]?.Trim() ?? "IT Admin",
            MustChangePassword = true,
            PasswordChangedAt = null,
            IsActive = true
        };

        var creationResult = await userManager.CreateAsync(user, temporaryPassword);

        if (!creationResult.Succeeded)
        {
            var errors = string.Join(" ", creationResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"The initial SystemAdmin could not be created. {errors}");
        }

        var systemAdminRole = await db.Roles
            .SingleAsync(role => role.Name == ApplicationRoles.SystemAdmin);

        db.UserRoles.Add(new AppUserRole
        {
            AppUserId = user.Id,
            AppRoleId = systemAdminRole.Id
        });

        await db.SaveChangesAsync();
    }
}
