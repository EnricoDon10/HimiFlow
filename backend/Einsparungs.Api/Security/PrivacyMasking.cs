namespace Einsparungs.Api.Security;

public static class PrivacyMasking
{
    public static string MaskKvnr(string? kvnr)
    {
        if (string.IsNullOrWhiteSpace(kvnr) || kvnr.Length < 4)
        {
            return "***";
        }

        return $"{kvnr[0]}******{kvnr[^3..]}";
    }
}
