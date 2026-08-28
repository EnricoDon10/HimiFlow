using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Data;

public class AppDbContext : IdentityUserContext<AppUser, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<SavingReason> SavingReasons => Set<SavingReason>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<SavingsEntry> SavingsEntries => Set<SavingsEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LicenseInstallation> LicenseInstallations => Set<LicenseInstallation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LockoutEnabled).HasDefaultValue(true);
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        modelBuilder.Entity<AppRole>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<Team>()
            .HasIndex(x => x.Code)
            .IsUnique();

        modelBuilder.Entity<AppUserRole>()
            .HasKey(x => new { x.AppUserId, x.AppRoleId });

        modelBuilder.Entity<AppUserRole>()
            .HasOne(x => x.AppUser)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.AppUserId);

        modelBuilder.Entity<AppUserRole>()
            .HasOne(x => x.AppRole)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.AppRoleId);

        modelBuilder.Entity<AppUser>()
            .HasOne(x => x.Team)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SavingsEntry>()
            .Property(x => x.OldKvAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<SavingsEntry>()
            .Property(x => x.NewKvAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<SavingsEntry>()
            .Property(x => x.SavingAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<SavingsEntry>()
            .HasOne(x => x.Team)
            .WithMany(x => x.SavingsEntries)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SavingsEntry>()
            .HasOne(x => x.SavingReason)
            .WithMany(x => x.SavingsEntries)
            .HasForeignKey(x => x.SavingReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SavingsEntry>()
            .HasOne(x => x.ProductGroup)
            .WithMany(x => x.SavingsEntries)
            .HasForeignKey(x => x.ProductGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SavingsEntry>()
            .HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedSavingsEntries)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SavingsEntry>()
            .HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SavingsEntry>()
            .HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SavingsEntry>()
            .ToTable(table =>
            {
                table.HasCheckConstraint("CK_SavingsEntry_OldKvAmount_NotNegative", "OldKvAmount >= 0");
                table.HasCheckConstraint("CK_SavingsEntry_NewKvAmount_NotNegative", "NewKvAmount >= 0");
                table.HasCheckConstraint("CK_SavingsEntry_NewKvAmount_LessOrEqual_OldKvAmount", "NewKvAmount <= OldKvAmount");
                table.HasCheckConstraint("CK_SavingsEntry_Kvnr_Length", "length(Kvnr) = 10");
            });

        modelBuilder.Entity<AuditLog>()
            .HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LicenseInstallation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LicenseKey).HasMaxLength(12000).IsRequired();
            entity.HasOne(x => x.InstalledByUser)
                .WithMany()
                .HasForeignKey(x => x.InstalledByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
