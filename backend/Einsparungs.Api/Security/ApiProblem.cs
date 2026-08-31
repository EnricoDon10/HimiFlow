using Microsoft.AspNetCore.Mvc;

namespace Einsparungs.Api.Security;

/// <summary>
/// Creates the small, consistent ProblemDetails envelope used by the API.
/// Domain-specific values remain extensions so clients can handle existing codes.
/// </summary>
public static class ApiProblem
{
    public static ProblemDetails Validation(HttpContext context, IEnumerable<string> errors) =>
        Create(
            context,
            StatusCodes.Status400BadRequest,
            "Validierungsfehler",
            "Bitte die Eingaben prüfen.",
            errors: errors);

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string title,
        string detail,
        string? code = null,
        IEnumerable<string>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (!string.IsNullOrWhiteSpace(code))
        {
            problem.Extensions["code"] = code;
        }

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return problem;
    }
}
