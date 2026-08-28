using Microsoft.AspNetCore.Identity;

namespace Einsparungs.Api.Security;

public sealed class GermanIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Die Aktion konnte nicht durchgeführt werden.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Der Datensatz wurde zwischenzeitlich geändert. Bitte erneut versuchen.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Das aktuelle Passwort ist falsch.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Der Sicherheitsnachweis ist ungültig oder abgelaufen.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), $"Der Benutzername '{userName}' ist bereits vergeben.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), $"Der Benutzername '{userName}' ist ungültig.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Das Passwort muss mindestens {length} Zeichen lang sein.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Das Passwort muss mindestens ein Sonderzeichen enthalten.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Das Passwort muss mindestens eine Ziffer enthalten.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Das Passwort muss mindestens einen Kleinbuchstaben enthalten.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Das Passwort muss mindestens einen Großbuchstaben enthalten.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Das Passwort muss mindestens {uniqueChars} unterschiedliche Zeichen enthalten.");

    private static IdentityError Error(string code, string description) => new()
    {
        Code = code,
        Description = description
    };
}
