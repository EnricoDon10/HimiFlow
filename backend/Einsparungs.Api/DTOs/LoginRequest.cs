using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public class LoginRequest
{
    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Password { get; set; } = string.Empty;
}
