using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Einsparungs.Api.Security;

public sealed class HimiFlowPasswordValidator : IPasswordValidator<AppUser>
{
    private static readonly string[] BlockedPasswords =
    [
        "password",
        "passwort",
        "himiflow",
        "viactiv",
        "qwertz",
        "123456"
    ];

    private readonly IPasswordHasher<AppUser> passwordHasher;

    public HimiFlowPasswordValidator(IPasswordHasher<AppUser> passwordHasher)
    {
        this.passwordHasher = passwordHasher;
    }

    public Task<IdentityResult> ValidateAsync(
        UserManager<AppUser> manager,
        AppUser user,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(IdentityResult.Failed(Error(
                "PasswordEmpty",
                "Das Passwort darf nicht leer sein.")));
        }

        var normalizedPassword = password.ToLowerInvariant();
        var errors = new List<IdentityError>();

        if (BlockedPasswords.Any(normalizedPassword.Contains))
        {
            errors.Add(Error(
                "PasswordCommon",
                "Das Passwort enthält einen leicht erratbaren Begriff oder eine einfache Zeichenfolge."));
        }

        var userName = user.UserName?.Trim();
        if (!string.IsNullOrWhiteSpace(userName) &&
            userName.Length >= 4 &&
            normalizedPassword.Contains(userName.ToLowerInvariant(), StringComparison.Ordinal))
        {
            errors.Add(Error(
                "PasswordContainsUserName",
                "Das Passwort darf den Benutzernamen nicht enthalten."));
        }

        var displayNameParts = user.DisplayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length >= 4);

        if (displayNameParts.Any(part => normalizedPassword.Contains(part.ToLowerInvariant(), StringComparison.Ordinal)))
        {
            errors.Add(Error(
                "PasswordContainsDisplayName",
                "Das Passwort darf keine längeren Bestandteile des Anzeigenamens enthalten."));
        }

        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification != PasswordVerificationResult.Failed)
            {
                errors.Add(Error(
                    "PasswordReused",
                    "Das neue Passwort muss sich vom aktuellen Passwort unterscheiden."));
            }
        }

        return Task.FromResult(errors.Count == 0
            ? IdentityResult.Success
            : IdentityResult.Failed(errors.ToArray()));
    }

    private static IdentityError Error(string code, string description) => new()
    {
        Code = code,
        Description = description
    };
}
