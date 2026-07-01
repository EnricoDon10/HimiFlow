using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.Models;

public class SavingReason
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<SavingsEntry> SavingsEntries { get; set; } = new List<SavingsEntry>();
}
