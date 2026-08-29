using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public sealed class ExportSavingsQuery
{
    public DateTime? Month { get; init; }

    [Range(1, int.MaxValue)]
    public int? TeamId { get; init; }

    [Range(1, int.MaxValue)]
    public int? SavingReasonId { get; init; }

    [Range(1, int.MaxValue)]
    public int? ProductGroupId { get; init; }
}
