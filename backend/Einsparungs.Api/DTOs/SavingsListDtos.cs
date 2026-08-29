using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public sealed class SavingsListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 50;

    public DateTime? Month { get; init; }

    [Range(1, int.MaxValue)]
    public int? TeamId { get; init; }

    [Range(1, int.MaxValue)]
    public int? SavingReasonId { get; init; }

    [Range(1, int.MaxValue)]
    public int? ProductGroupId { get; init; }

    public Guid? CreatedByUserId { get; init; }
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
