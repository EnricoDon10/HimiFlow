using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Einsparungs.Api.Security;

public sealed class PasswordChangeRequiredMiddleware
{
    private static readonly PathString[] AllowedPaths =
    [
        new("/api/auth/me"),
        new("/api/auth/change-password"),
        new("/api/auth/logout"),
        new("/api/auth/csrf")
    ];

    private readonly RequestDelegate next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var passwordChangeRequired = string.Equals(
            context.User.FindFirstValue(SecurityClaims.MustChangePassword),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

        if (
            context.User.Identity?.IsAuthenticated == true &&
            passwordChangeRequired &&
            context.Request.Path.StartsWithSegments("/api") &&
            !AllowedPaths.Any(path => context.Request.Path.Equals(path)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Passwortänderung erforderlich",
                Detail = "Vor der Nutzung der Anwendung muss ein persönliches Passwort vergeben werden.",
                Extensions =
                {
                    ["code"] = "PASSWORD_CHANGE_REQUIRED",
                    ["traceId"] = context.TraceIdentifier
                }
            });
            return;
        }

        await next(context);
    }
}
