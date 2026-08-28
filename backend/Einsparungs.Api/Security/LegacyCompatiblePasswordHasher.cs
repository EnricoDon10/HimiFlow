using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Einsparungs.Api.Security;

public sealed class LegacyCompatiblePasswordHasher : IPasswordHasher<AppUser>
{
    private readonly PasswordHasher<AppUser> identityHasher = new();

    public string HashPassword(AppUser user, string password)
    {
        return identityHasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        AppUser user,
        string hashedPassword,
        string providedPassword)
    {
        if (hashedPassword.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword)
                    ? PasswordVerificationResult.SuccessRehashNeeded
                    : PasswordVerificationResult.Failed;
            }
            catch
            {
                return PasswordVerificationResult.Failed;
            }
        }

        return identityHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
