using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public class LoginRequest
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}