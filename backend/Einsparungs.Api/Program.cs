using System.Net;
using System.Threading.RateLimiting;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var databaseProvider = builder.Configuration["Database:Provider"]?.Trim() ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection ist nicht konfiguriert.");
var requireHttps = builder.Configuration.GetValue(
    "Security:RequireHttps",
    !builder.Environment.IsDevelopment());
var reverseProxyEnabled = builder.Configuration.GetValue("ReverseProxy:Enabled", false);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long>(
        "Security:MaxRequestBodySizeBytes",
        1_048_576);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "Eine Eingabe ist ungültig."
                : error.ErrorMessage)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validierungsfehler",
            Detail = "Bitte die markierten Eingaben prüfen.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["errors"] = errors;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem);
    };
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(databaseProvider, "SQLite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
        return;
    }

    if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString, sqlServer =>
        {
            sqlServer.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sqlServer.CommandTimeout(30);
        });
        return;
    }

    throw new InvalidOperationException(
        $"Database:Provider '{databaseProvider}' wird nicht unterstützt. Zulässig sind SQLite und SqlServer.");
});

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequiredLength = builder.Configuration.GetValue(
            "Identity:Password:RequiredLength",
            14);
        options.Password.RequiredUniqueChars = builder.Configuration.GetValue(
            "Identity:Password:RequiredUniqueChars",
            4);
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = builder.Configuration.GetValue(
            "Identity:Lockout:MaxFailedAccessAttempts",
            5);
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue("Identity:Lockout:Minutes", 15));
    })
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddPasswordValidator<HimiFlowPasswordValidator>()
    .AddErrorDescriber<GermanIdentityErrorDescriber>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();

builder.Services.Configure<CookieAuthenticationOptions>(
    IdentityConstants.ApplicationScheme,
    options =>
    {
        options.Cookie.Name = "HimiFlow.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = requireHttps
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue("Identity:Session:IdleMinutes", 30));
        options.SlidingExpiration = true;
        options.EventsType = typeof(ActiveUserCookieEvents);
    });

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "HimiFlow.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = requireHttps
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddScoped<IPasswordHasher<AppUser>, LegacyCompatiblePasswordHasher>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();
builder.Services.AddScoped<ActiveUserCookieEvents>();
builder.Services.AddSingleton<TemporaryPasswordGenerator>();
builder.Services.AddScoped<OfflineLicenseValidator>();
builder.Services.AddScoped<LicenseService>();
builder.Services.AddScoped<LicenseReadOnlyMiddleware>();
builder.Services.AddScoped<SqliteBackupService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<SqliteBackupBackgroundService>();
builder.Services.AddHealthChecks()
    .AddCheck<LocalHealthCheck>("database", tags: new[] { "ready" });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(180);
    options.IncludeSubDomains = false;
    options.Preload = false;
});
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = builder.Configuration.GetValue<int?>("Security:HttpsPort") ?? 443;
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
});

if (reverseProxyEnabled)
{
    var knownProxyValues = builder.Configuration
        .GetSection("ReverseProxy:KnownProxies")
        .Get<string[]>() ?? Array.Empty<string>();

    if (knownProxyValues.Length == 0)
    {
        throw new InvalidOperationException(
            "ReverseProxy:Enabled ist aktiv, aber ReverseProxy:KnownProxies enthält keine vertrauenswürdige Proxy-IP.");
    }

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var value in knownProxyValues)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownProxies enthält keine gültige IP-Adresse: {value}");
            }

            options.KnownProxies.Add(address);
        }
    });
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevClient", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

var migrateCommand = args.Any(argument => string.Equals(argument, "--migrate", StringComparison.OrdinalIgnoreCase));
var seedCommand = args.Any(argument => string.Equals(argument, "--seed", StringComparison.OrdinalIgnoreCase));
var backupNowCommand = args.Any(argument => string.Equals(argument, "--backup-now", StringComparison.OrdinalIgnoreCase));
var validateBackupPath = GetArgumentValue(args, "--validate-backup");
var applyMigrationsOnStartup = builder.Configuration.GetValue(
    "Database:ApplyMigrationsOnStartup",
    builder.Environment.IsDevelopment());
var seedOnStartup = builder.Configuration.GetValue(
    "Database:SeedOnStartup",
    builder.Environment.IsDevelopment());

using (var scope = app.Services.CreateScope())
{
    if (backupNowCommand || validateBackupPath is not null)
    {
        var backupService = scope.ServiceProvider.GetRequiredService<SqliteBackupService>();

        if (validateBackupPath is not null)
        {
            var result = await backupService.ValidateAsync(validateBackupPath);
            if (!result.IsValid)
            {
                throw new InvalidOperationException($"Backup-Integritätsprüfung fehlgeschlagen: {result.Result}");
            }

            Console.WriteLine($"SQLite-Backup ist gültig: {Path.GetFullPath(validateBackupPath)}");
            return;
        }

        var backup = await backupService.CreateAsync();
        Console.WriteLine($"SQLite-Backup erstellt: {backup.FullPath}");
        return;
    }

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    if (migrateCommand || seedCommand)
    {
        if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Die aktuellen Migrationen sind die SQLite-Migrationshistorie. SQL-Server-Migrationen werden erst in der Phase Inbetriebnahme erzeugt und abgenommen.");
        }

        await db.Database.MigrateAsync();
        if (seedCommand)
        {
            await DatabaseSeeder.SeedAsync(
                db,
                userManager,
                app.Configuration,
                applyMigrations: false,
                seedReferenceData: true);
        }

        return;
    }

    await DatabaseSeeder.SeedAsync(
        db,
        userManager,
        app.Configuration,
        applyMigrations: applyMigrationsOnStartup,
        seedReferenceData: seedOnStartup);
}

if (reverseProxyEnabled)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (requireHttps)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var response = httpContext.Response;

    if (response.HasStarted || response.ContentLength > 0 || !string.IsNullOrWhiteSpace(response.ContentType))
    {
        return;
    }

    var (title, detail) = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => ("Nicht angemeldet", "Für diese Anfrage ist eine Anmeldung erforderlich."),
        StatusCodes.Status403Forbidden => ("Zugriff verweigert", "Für diese Aktion fehlt die erforderliche Berechtigung."),
        StatusCodes.Status404NotFound => ("Nicht gefunden", "Die angeforderte Ressource wurde nicht gefunden."),
        StatusCodes.Status405MethodNotAllowed => ("Methode nicht erlaubt", "Die HTTP-Methode ist für diese Ressource nicht zulässig."),
        StatusCodes.Status429TooManyRequests => ("Zu viele Anfragen", "Bitte warten und die Anfrage später erneut versuchen."),
        _ => ("Anfrage fehlgeschlagen", "Die Anfrage konnte nicht verarbeitet werden.")
    };
    var problem = new ProblemDetails
    {
        Status = response.StatusCode,
        Title = title,
        Detail = detail,
        Instance = httpContext.Request.Path
    };
    problem.Extensions["traceId"] = httpContext.TraceIdentifier;
    response.ContentType = "application/problem+json";
    await response.WriteAsJsonAsync(problem);
});
app.UseMiddleware<CorrelationIdMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd(
        "Permissions-Policy",
        "camera=(), microphone=(), geolocation=()");

    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.TryAdd(
            "Content-Security-Policy",
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
    }

    await next();
});

app.UseRouting();
app.UseCors("AngularDevClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseMiddleware<LicenseReadOnlyMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapHealthChecks("/api/health");
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapGet("/api/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/", () => "Einsparungs API laeuft.");

app.Run();

static string? GetArgumentValue(string[] arguments, string argumentName)
{
    for (var index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], argumentName, StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

public partial class Program;
