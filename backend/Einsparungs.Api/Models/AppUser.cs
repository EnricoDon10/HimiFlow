using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.Models;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
    public ICollection<SavingsEntry> CreatedSavingsEntries { get; set; } = new List<SavingsEntry>();

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }}

