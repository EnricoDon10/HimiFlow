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
}
