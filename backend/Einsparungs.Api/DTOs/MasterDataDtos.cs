namespace Einsparungs.Api.DTOs;

// Code/Name bleiben für bestehende API-Clients kompatibel. Die Local Edition
// verwendet für neue Eingaben ausschließlich OrganizationUnit als einheitlichen
// fachlichen Wert.
public sealed record TeamSaveRequest(
    string? Code = null,
    string? Name = null,
    string? OrganizationUnit = null);

public sealed record TeamResponse(
    int Id,
    string Code,
    string Name,
    string DisplayName,
    bool IsActive,
    int ActiveUserCount);

public sealed record SavingReasonSaveRequest(string Name);

public sealed record SavingReasonResponse(
    int Id,
    string Name,
    bool IsActive,
    int ReferencedSavingsCount);

public sealed record ProductGroupSaveRequest(string DisplayValue);

public sealed record ProductGroupResponse(
    int Id,
    string DisplayValue,
    bool IsActive,
    int ReferencedSavingsCount);
