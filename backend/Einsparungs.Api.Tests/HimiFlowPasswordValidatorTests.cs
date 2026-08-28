using Einsparungs.Api.Models;
using Einsparungs.Api.Security;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class HimiFlowPasswordValidatorTests
{
    private readonly LegacyCompatiblePasswordHasher hasher = new();

    [TestMethod]
    public async Task ValidateAsync_AcceptsStrongUnrelatedPassword()
    {
        var user = CreateUser();
        var validator = new HimiFlowPasswordValidator(hasher);

        var result = await validator.ValidateAsync(null!, user, "L7!kR2@vP9#xT4");

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public async Task ValidateAsync_RejectsUserNameAndCommonProductTerms()
    {
        var user = CreateUser();
        var validator = new HimiFlowPasswordValidator(hasher);

        var result = await validator.ValidateAsync(null!, user, "Enrico.HimiFlow-2026!");

        Assert.IsFalse(result.Succeeded);
        CollectionAssert.Contains(result.Errors.Select(error => error.Code).ToArray(), "PasswordCommon");
        CollectionAssert.Contains(result.Errors.Select(error => error.Code).ToArray(), "PasswordContainsDisplayName");
    }

    [TestMethod]
    public async Task ValidateAsync_RejectsCurrentPasswordReuse()
    {
        var user = CreateUser();
        const string currentPassword = "R8!mT2@qW7#zP4";
        user.PasswordHash = hasher.HashPassword(user, currentPassword);
        var validator = new HimiFlowPasswordValidator(hasher);

        var result = await validator.ValidateAsync(null!, user, currentPassword);

        Assert.IsFalse(result.Succeeded);
        CollectionAssert.Contains(result.Errors.Select(error => error.Code).ToArray(), "PasswordReused");
    }

    private static AppUser CreateUser() => new()
    {
        UserName = "enrico.mancuso",
        DisplayName = "Enrico Mancuso"
    };
}
