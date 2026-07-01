namespace Einsparungs.Api.DTOs;

public class GroupedSavingsStatisticResponse
{
    public string GroupKey { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public int EntryCount { get; set; }

    public decimal TotalSavingAmount { get; set; }

    public decimal AverageSavingAmount { get; set; }
}
