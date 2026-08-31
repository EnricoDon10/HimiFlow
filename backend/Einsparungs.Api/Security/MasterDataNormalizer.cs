namespace Einsparungs.Api.Security;

/// <summary>
/// Applies the deliberately small normalization contract for master data.
/// Storage keeps the user-facing spelling; comparisons ignore surrounding
/// whitespace and casing.
/// </summary>
public static class MasterDataNormalizer
{
    public static string ForStorage(string? value) => value?.Trim() ?? string.Empty;

    public static string ForComparison(string? value) =>
        ForStorage(value).ToUpperInvariant();
}
