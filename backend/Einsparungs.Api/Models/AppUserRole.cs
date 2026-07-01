namespace Einsparungs.Api.Models;

public class AppUserRole
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    public int AppRoleId { get; set; }
    public AppRole AppRole { get; set; } = null!;
}
