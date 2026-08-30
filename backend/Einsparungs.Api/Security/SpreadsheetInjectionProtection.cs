namespace Einsparungs.Api.Security;

public static class SpreadsheetInjectionProtection
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    /// <summary>Neutralizes text values that spreadsheet applications may interpret as formulas.</summary>
    public static string NeutralizeText(string? value)
    {
        var text = value ?? string.Empty;
        var formulaCandidate = text.TrimStart(' ', '\t', '\r', '\n');
        return formulaCandidate.Length > 0 && FormulaPrefixes.Contains(formulaCandidate[0])
            ? $"'{text}"
            : text;
    }
}
