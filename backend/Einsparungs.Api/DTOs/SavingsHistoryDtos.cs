namespace Einsparungs.Api.DTOs;

public sealed record SavingsHistoryEntryResponse(
    long Id,
    string Action,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    string ChangedByDisplayName,
    IReadOnlyList<SavingsFieldChangeResponse> Changes);

public sealed record SavingsFieldChangeResponse(
    string Field,
    string Label,
    string? OldValue,
    string? NewValue);
