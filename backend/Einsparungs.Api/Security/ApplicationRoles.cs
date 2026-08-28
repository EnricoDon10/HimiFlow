namespace Einsparungs.Api.Security;

public static class ApplicationRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string FachAdmin = "FachAdmin";
    public const string Mitarbeiter = "Mitarbeiter";

    public static readonly IReadOnlyCollection<string> All =
    [
        SystemAdmin,
        FachAdmin,
        Mitarbeiter
    ];
}
