using Einsparungs.Api.Security;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class PrivacyMaskingTests
{
    [TestMethod]
    public void MaskKvnr_KeepsOnlyFirstCharacterAndLastThreeDigits()
    {
        Assert.AreEqual("A******789", PrivacyMasking.MaskKvnr("A123456789"));
    }

    [TestMethod]
    public void MaskKvnr_HandlesMissingValues()
    {
        Assert.AreEqual("***", PrivacyMasking.MaskKvnr(null));
        Assert.AreEqual("***", PrivacyMasking.MaskKvnr("123"));
    }
}
