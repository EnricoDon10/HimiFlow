using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.Models;

public class ProductGroup
{
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string DisplayValue { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ImportedBy { get; set; }

    public ICollection<SavingsEntry> SavingsEntries { get; set; } = new List<SavingsEntry>();
}
