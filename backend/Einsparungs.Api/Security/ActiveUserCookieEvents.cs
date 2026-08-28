using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Einsparungs.Api.Security;

public sealed class ActiveUserCookieEvents : CookieAuthenticationEvents
{
    private readonly ISecurityStampValidator securityStampValidator;
    private readonly UserManager<AppUser> userManager;

    public ActiveUserCookieEvents(
        ISecurityStampValidator securityStampValidator,
        UserManager<AppUser> userManager)
    {
        this.securityStampValidator = securityStampValidator;
        this.userManager = userManager;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        await securityStampValidator.ValidateAsync(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = await userManager.GetUserAsync(context.Principal);

        if (user is not null && user.IsActive && !user.IsDeleted)
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
