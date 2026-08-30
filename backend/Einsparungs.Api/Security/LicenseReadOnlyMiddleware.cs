using Microsoft.AspNetCore.Mvc;

namespace Einsparungs.Api.Security;

public sealed class LicenseReadOnlyMiddleware : IMiddleware
{
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
            IsRecoveryWriteAllowed(context.Request))
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

    private static bool IsRecoveryWriteAllowed(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? string.Empty;
        if (path is "/api/auth/logout" or "/api/auth/change-password" or
            "/api/admin/license" or "/api/operations/backups")
        {
            return true;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 5 &&
            string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "operations", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[2], "backups", StringComparison.OrdinalIgnoreCase) &&
            segments[4] is "validate" or "prepare-restore")
        {
            return true;
        }

        return segments.Length == 4 &&
               string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "user-management", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(segments[2], out _) &&
               segments[3] is "reset-password" or "deactivate";
    }
}
