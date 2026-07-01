namespace Einsparungs.Api.DTOs;

public class MonthlySavingsStatisticResponse
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthLabel { get; set; } = string.Empty;

    public int EntryCount { get; set; }

    public decimal TotalSavingAmount { get; set; }

    public decimal AverageSavingAmount { get; set; }
}
