using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.Models;

public class SavingsEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Month { get; set; }

    [Required]
    [MaxLength(10)]
    public string Kvnr { get; set; } = string.Empty;

    public decimal OldKvAmount { get; set; }

    public decimal NewKvAmount { get; set; }

    public decimal SavingAmount { get; set; }

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public int SavingReasonId { get; set; }
    public SavingReason SavingReason { get; set; } = null!;

    public int ProductGroupId { get; set; }
    public ProductGroup ProductGroup { get; set; } = null!;

    public DateTime TransmissionDate { get; set; } = DateTime.UtcNow;

    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }
    public AppUser? UpdatedByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public Guid? DeletedByUserId { get; set; }
    public AppUser? DeletedByUser { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int Version { get; set; } = 1;
}
