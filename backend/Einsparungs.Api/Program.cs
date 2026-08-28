using System.Threading.RateLimiting;
using Einsparungs.Api.Data;
using Einsparungs.Api.Models;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<AppDbContext>();

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
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
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
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddScoped<IPasswordHasher<AppUser>, LegacyCompatiblePasswordHasher>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();
builder.Services.AddScoped<ActiveUserCookieEvents>();
builder.Services.AddSingleton<TemporaryPasswordGenerator>();
builder.Services.AddScoped<OfflineLicenseValidator>();
builder.Services.AddScoped<LicenseService>();
builder.Services.AddScoped<LicenseReadOnlyMiddleware>();
builder.Services.AddScoped<SqliteBackupService>();
builder.Services.AddHealthChecks()
    .AddCheck<LocalHealthCheck>("database", tags: new[] { "ready" });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
var applyMigrationsOnStartup = builder.Configuration.GetValue(
    "Database:ApplyMigrationsOnStartup",
    builder.Environment.IsDevelopment());
var seedOnStartup = builder.Configuration.GetValue(
    "Database:SeedOnStartup",
    builder.Environment.IsDevelopment());

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    if (migrateCommand || seedCommand)
    {
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
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

public partial class Program;
