using Einsparungs.Api.Security;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class SpreadsheetInjectionProtectionTests
{
    [TestMethod]
    [DataRow("=HYPERLINK(\"https://example.invalid\")")]
    [DataRow("+SUM(1,2)")]
    [DataRow("-cmd|' /C calc'!A0")]
    [DataRow("@SUM(1,2)")]
    public void FormulaPrefixesAreNeutralized(string value)
    {
        var safe = SpreadsheetInjectionProtection.NeutralizeText(value);

        Assert.AreEqual($"'{value}", safe);
    }

    [TestMethod]
    public void OrdinaryTextIsNotChanged()
    {
        Assert.AreEqual("Bochum 1", SpreadsheetInjectionProtection.NeutralizeText("Bochum 1"));
    }

    [TestMethod]
    [DataRow("\t=SUM(1,2)")]
    [DataRow("\r=HYPERLINK(\"https://example.invalid\")")]
    [DataRow("\n+SUM(1,2)")]
    [DataRow("  @SUM(1,2)")]
    public void ControlCharactersBeforeFormulaAreNeutralized(string value)
    {
        Assert.AreEqual($"'{value}", SpreadsheetInjectionProtection.NeutralizeText(value));
    }

    [TestMethod]
    public void LeadingTabInOrdinaryTextIsPreserved()
    {
        Assert.AreEqual("\tHinweis", SpreadsheetInjectionProtection.NeutralizeText("\tHinweis"));
    }
}
