using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Einsparungs.Api.Models;

public class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.NewGuid();
        LockoutEnabled = true;
    }

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;

    public DateTime? PasswordChangedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
    public ICollection<SavingsEntry> CreatedSavingsEntries { get; set; } = new List<SavingsEntry>();

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
