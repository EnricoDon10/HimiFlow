using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.Models;

public class AuditLog
{
    public long Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    public Guid ChangedByUserId { get; set; }
    public AppUser ChangedByUser { get; set; } = null!;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public string? ChangedFieldsJson { get; set; }

    [MaxLength(50)]
    public string? ClientIp { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }
}
