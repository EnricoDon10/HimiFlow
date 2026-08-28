namespace Einsparungs.Api.DTOs;

public sealed record ProductInfoResponse(
    string ProductName,
    string Edition,
    string Version,
    LegalNoticeResponse LegalNotice);

public sealed record LegalNoticeResponse(
    bool IsConfigured,
    string? ProviderName,
    string? LegalForm,
    IReadOnlyList<string> AddressLines,
    string? Email,
    string? Phone,
    string? Website,
    string? RegisterCourt,
    string? RegisterNumber,
    string? VatId,
    string? PrivacyContact);
