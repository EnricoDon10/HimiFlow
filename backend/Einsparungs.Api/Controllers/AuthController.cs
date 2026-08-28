using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string ReadableAntiforgeryCookieName = "XSRF-TOKEN";

    private readonly AppDbContext db;
    private readonly UserManager<AppUser> userManager;
    private readonly SignInManager<AppUser> signInManager;
    private readonly IAntiforgery antiforgery;
    private readonly IWebHostEnvironment environment;

    public AuthController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IAntiforgery antiforgery,
        IWebHostEnvironment environment)
    {
        this.db = db;
        this.userManager = userManager;
        this.signInManager = signInManager;
        this.antiforgery = antiforgery;
        this.environment = environment;
    }

    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        IssueReadableAntiforgeryToken();

        return NoContent();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var userName = request.UserName.Trim();
        var user = await userManager.FindByNameAsync(userName);

        if (user is null || !user.IsActive || user.IsDeleted)
        {
            return InvalidLogin();
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return InvalidLogin();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        // Antiforgery tokens are bound to the current principal. Refresh the
        // token after the anonymous login request has established the cookie.
        IssueReadableAntiforgeryToken();
        return Ok(await CreateLoginResponseAsync(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null || !user.IsActive || user.IsDeleted)
        {
            await signInManager.SignOutAsync();
            return Unauthorized();
        }

        return Ok(await CreateLoginResponseAsync(user));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { errors = new[] { "Die neuen Passwörter stimmen nicht überein." } });
        }

        var user = await userManager.GetUserAsync(User);

        if (user is null || !user.IsActive || user.IsDeleted)
        {
            return Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(error => error.Description).ToArray()
            });
        }

        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                errors = updateResult.Errors.Select(error => error.Description).ToArray()
            });
        }

        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);

        return Ok(await CreateLoginResponseAsync(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        Response.Cookies.Delete(ReadableAntiforgeryCookieName, new CookieOptions { Path = "/" });
        return NoContent();
    }

    private UnauthorizedObjectResult InvalidLogin()
    {
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Anmeldung fehlgeschlagen",
            Detail = "Benutzername oder Passwort ist falsch oder die Anmeldung ist vorübergehend gesperrt."
        });
    }

    private void IssueReadableAntiforgeryToken()
    {
        var tokenSet = antiforgery.GetAndStoreTokens(HttpContext);

        Response.Cookies.Append(
            ReadableAntiforgeryCookieName,
            tokenSet.RequestToken ?? string.Empty,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                Path = "/",
                SameSite = SameSiteMode.Strict,
                Secure = !environment.IsDevelopment()
            });
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(AppUser user)
    {
        var roles = await db.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.AppUserId == user.Id)
            .Select(userRole => userRole.AppRole.Name)
            .Distinct()
            .OrderBy(role => role)
            .ToListAsync();

        return new LoginResponse
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            DisplayName = user.DisplayName,
            Roles = roles,
            MustChangePassword = user.MustChangePassword
        };
    }
}
