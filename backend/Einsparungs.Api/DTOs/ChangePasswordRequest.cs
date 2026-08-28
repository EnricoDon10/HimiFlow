using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.DTOs;

public sealed class ChangePasswordRequest
{
    [Required]
    [StringLength(200)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 14)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 14)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
