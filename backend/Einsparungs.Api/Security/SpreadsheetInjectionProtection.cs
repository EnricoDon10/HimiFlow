namespace Einsparungs.Api.Security;

public static class SpreadsheetInjectionProtection
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    /// <summary>Neutralizes text values that spreadsheet applications may interpret as formulas.</summary>
    public static string NeutralizeText(string? value)
    {
        var text = value ?? string.Empty;
        return text.Length > 0 && FormulaPrefixes.Contains(text[0])
            ? $"'{text}"
            : text;
    }
}
