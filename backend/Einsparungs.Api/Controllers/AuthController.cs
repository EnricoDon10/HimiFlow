using System.Security.Claims;
using Einsparungs.Api.Data;
using Einsparungs.Api.DTOs;
using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(AppDbContext db, JwtTokenService jwtTokenService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.AppRole)
            .SingleOrDefaultAsync(x => x.UserName == request.UserName && x.IsActive);

        if (user is null)
        {
            return Unauthorized("Benutzername oder Passwort ist falsch.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            return Unauthorized("Benutzername oder Passwort ist falsch.");
        }

        var roles = user.UserRoles
            .Select(x => x.AppRole.Name)
            .OrderBy(x => x)
            .ToList();

        var tokenResult = _jwtTokenService.CreateToken(user, roles);

        return Ok(new LoginResponse
        {
            Token = tokenResult.Token,
            ExpiresAt = tokenResult.ExpiresAt,
            UserId = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Roles = roles
        });
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var userId = User.FindFirst("userId")?.Value;
        var userName = User.FindFirst("userName")?.Value;
        var displayName = User.FindFirst("displayName")?.Value;

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Union(User.FindAll("role").Select(x => x.Value))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return Ok(new
        {
            UserId = userId,
            UserName = userName,
            DisplayName = displayName,
            Roles = roles
        });
    }
}
