using Microsoft.AspNetCore.Mvc;

namespace Einsparungs.Api.Security;

public sealed class LicenseReadOnlyMiddleware : IMiddleware
{
    private static readonly PathString[] AlwaysAllowedPaths =
    [
        new("/api/auth"),
        new("/api/license"),
        new("/api/admin/license"),
        new("/api/user-management"),
        new("/api/operations"),
        new("/api/health")
    ];

    private readonly LicenseService licenseService;

    public LicenseReadOnlyMiddleware(LicenseService licenseService)
    {
        this.licenseService = licenseService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!licenseService.IsEnforcementEnabled ||
            context.User.Identity?.IsAuthenticated != true ||
            IsSafeRequest(context) ||
            IsAlwaysAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        var validation = await licenseService.ValidateCurrentAsync(context.RequestAborted);

        if (validation.IsValidForOperation)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Lizenz erlaubt nur Lesebetrieb",
            Detail = validation.Error ?? "Für diese Installation ist kein aktiver Schreibbetrieb freigeschaltet.",
            Extensions =
            {
                ["code"] = "LICENSE_READ_ONLY",
                ["licenseStatus"] = validation.Status,
                ["traceId"] = context.TraceIdentifier
            }
        }, context.RequestAborted);
    }

    private static bool IsSafeRequest(HttpContext context)
    {
        return context.Request.Method is "GET" or "HEAD" or "OPTIONS";
    }

    private static bool IsAlwaysAllowed(PathString path)
    {
        return AlwaysAllowedPaths.Any(allowed => path.StartsWithSegments(allowed));
    }
}
