using Einsparungs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<SavingReason> SavingReasons => Set<SavingReason>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<SavingsEntry> SavingsEntries => Set<SavingsEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .HasIndex(x => x.UserName)
            .IsUnique();

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
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_SavingsEntry_OldKvAmount_NotNegative", "OldKvAmount >= 0");
                t.HasCheckConstraint("CK_SavingsEntry_NewKvAmount_NotNegative", "NewKvAmount >= 0");
                t.HasCheckConstraint("CK_SavingsEntry_NewKvAmount_LessOrEqual_OldKvAmount", "NewKvAmount <= OldKvAmount");
                t.HasCheckConstraint("CK_SavingsEntry_Kvnr_Length", "length(Kvnr) = 10");
            });

        modelBuilder.Entity<AuditLog>()
            .HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}