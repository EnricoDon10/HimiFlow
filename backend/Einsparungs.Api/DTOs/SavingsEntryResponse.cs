namespace Einsparungs.Api.DTOs;

public class SavingsEntryResponse
{
    public Guid Id { get; set; }

    public DateTime Month { get; set; }

    public string Kvnr { get; set; } = string.Empty;

    public decimal OldKvAmount { get; set; }

    public decimal NewKvAmount { get; set; }

    public decimal SavingAmount { get; set; }

    public int TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public int SavingReasonId { get; set; }

    public string SavingReasonName { get; set; } = string.Empty;

    public int ProductGroupId { get; set; }

    public string ProductGroupDisplayValue { get; set; } = string.Empty;

    public DateTime TransmissionDate { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByUserName { get; set; } = string.Empty;

    public string CreatedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int Version { get; set; }
}
