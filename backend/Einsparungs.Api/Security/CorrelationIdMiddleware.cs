namespace Einsparungs.Api.Security;

public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate next;
    private readonly ILogger<CorrelationIdMiddleware> logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsSafeCorrelationId(supplied)
            ? supplied!
            : context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static bool IsSafeCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            return false;
        }

        return value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}
