using System.Security.Claims;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Einsparungs.Api.Security;

public sealed class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser>
{
    private readonly AppDbContext db;

    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        IOptions<IdentityOptions> optionsAccessor,
        AppDbContext db)
        : base(userManager, optionsAccessor)
    {
        this.db = db;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(SecurityClaims.UserId, user.Id.ToString()));
        identity.AddClaim(new Claim(SecurityClaims.DisplayName, user.DisplayName));
        identity.AddClaim(new Claim(
            SecurityClaims.MustChangePassword,
            user.MustChangePassword ? bool.TrueString : bool.FalseString));

        var roles = await db.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.AppUserId == user.Id)
            .Select(userRole => userRole.AppRole.Name)
            .Distinct()
            .ToListAsync();

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return identity;
    }
}
