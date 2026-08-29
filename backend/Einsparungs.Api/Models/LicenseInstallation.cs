using System.ComponentModel.DataAnnotations;

namespace Einsparungs.Api.Models;

/// <summary>
/// Stores the currently installed offline license. The license itself is signed
/// by the vendor; this table only stores the customer-provided license token.
/// </summary>
public sealed class LicenseInstallation
{
    public int Id { get; set; } = 1;

    [Required]
    [MaxLength(12000)]
    public string LicenseKey { get; set; } = string.Empty;

    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSuccessfulLicenseValidationUtc { get; set; }

    public Guid? InstalledByUserId { get; set; }

    public AppUser? InstalledByUser { get; set; }
}
