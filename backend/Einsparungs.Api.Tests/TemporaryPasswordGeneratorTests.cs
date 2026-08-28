using Einsparungs.Api.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class TemporaryPasswordGeneratorTests
{
    [TestMethod]
    public void Generate_ProducesPolicyCompatiblePassword()
    {
        var password = new TemporaryPasswordGenerator().Generate();

        Assert.AreEqual(20, password.Length);
        StringAssert.Matches(password, new System.Text.RegularExpressions.Regex("[A-Z]"));
        StringAssert.Matches(password, new System.Text.RegularExpressions.Regex("[a-z]"));
        StringAssert.Matches(password, new System.Text.RegularExpressions.Regex("[0-9]"));
        StringAssert.Matches(password, new System.Text.RegularExpressions.Regex("[^A-Za-z0-9]"));
    }

    [TestMethod]
    public void Generate_ProducesDifferentValues()
    {
        var generator = new TemporaryPasswordGenerator();
        var passwords = Enumerable.Range(0, 20)
            .Select(_ => generator.Generate())
            .ToHashSet(StringComparer.Ordinal);

        Assert.AreEqual(20, passwords.Count);
    }

    [TestMethod]
    public void Generate_RejectsShortPasswords()
    {
        var threw = false;

        try
        {
            new TemporaryPasswordGenerator().Generate(11);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }
}
