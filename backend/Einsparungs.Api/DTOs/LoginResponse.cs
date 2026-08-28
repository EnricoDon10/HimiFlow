namespace Einsparungs.Api.DTOs;

public sealed class LoginResponse
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = new();

    public bool MustChangePassword { get; set; }
}
