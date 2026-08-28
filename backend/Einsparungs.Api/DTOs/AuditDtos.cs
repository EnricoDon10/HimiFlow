namespace Einsparungs.Api.DTOs;

public sealed record AuditLogResponse(
    long Id,
    string EntityName,
    string EntityId,
    string Action,
    Guid ChangedByUserId,
    string? ChangedByUserName,
    DateTime ChangedAt,
    string? ClientIp,
    string? UserAgent,
    IReadOnlyList<string> ChangedFields,
    bool HasOldValues,
    bool HasNewValues);

public sealed record AuditLogPageResponse(
    IReadOnlyList<AuditLogResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
