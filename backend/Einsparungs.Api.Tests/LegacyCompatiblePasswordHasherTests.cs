using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class LegacyCompatiblePasswordHasherTests
{
    private readonly LegacyCompatiblePasswordHasher hasher = new();
    private readonly AppUser user = new() { UserName = "test-user", DisplayName = "Test User" };

    [TestMethod]
    public void VerifyHashedPassword_RecognizesLegacyBcryptAndRequestsRehash()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Legacy-Password-2026!");

        var result = hasher.VerifyHashedPassword(user, hash, "Legacy-Password-2026!");

        Assert.AreEqual(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [TestMethod]
    public void VerifyHashedPassword_RejectsWrongLegacyPassword()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Legacy-Password-2026!");

        var result = hasher.VerifyHashedPassword(user, hash, "wrong-password");

        Assert.AreEqual(PasswordVerificationResult.Failed, result);
    }

    [TestMethod]
    public void VerifyHashedPassword_DelegatesIdentityHashes()
    {
        var hash = hasher.HashPassword(user, "Identity-Password-2026!");

        var result = hasher.VerifyHashedPassword(user, hash, "Identity-Password-2026!");

        Assert.AreEqual(PasswordVerificationResult.Success, result);
    }
}
