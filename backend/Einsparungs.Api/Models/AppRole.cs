using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.Models;

public class AppRole
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}
