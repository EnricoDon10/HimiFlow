using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public class SavingsEntryCreateRequest
{
    [Required]
    public DateTime Month { get; set; }

    [Required]
    [MaxLength(10)]
    public string Kvnr { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal OldKvAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal NewKvAmount { get; set; }

    [Required]
    public int TeamId { get; set; }

    [Required]
    public int SavingReasonId { get; set; }

    [Required]
    public int ProductGroupId { get; set; }
}
