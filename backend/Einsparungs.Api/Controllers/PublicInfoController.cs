using System.Reflection;
using Einsparungs.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einsparungs.Api.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicInfoController : ControllerBase
{
    private readonly IConfiguration configuration;

    public PublicInfoController(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    [HttpGet("product-info")]
    public ActionResult<ProductInfoResponse> GetProductInfo()
    {
        var providerName = Normalize(configuration["Legal:ProviderName"]);
        var email = Normalize(configuration["Legal:Email"]);
        var addressLines = NormalizeList(
            configuration.GetSection("Legal:AddressLines").Get<string[]>() ?? Array.Empty<string>());
        var phoneNumbers = ReadList("Legal:PhoneNumbers");
        var legacyPhone = Normalize(configuration["Legal:Phone"]);
        if (phoneNumbers.Length == 0 && legacyPhone is not null)
        {
            phoneNumbers = [legacyPhone];
        }
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "0.9.0-rc.1";

        return Ok(new ProductInfoResponse(
            configuration["Product:Name"] ?? "HimiFlow Einsparungsdatenbank",
            configuration["Product:Edition"] ?? "Local Edition",
            version,
            new LegalNoticeResponse(
                !string.IsNullOrWhiteSpace(providerName) &&
                !string.IsNullOrWhiteSpace(email) &&
                addressLines.Length > 0,
                providerName,
                Normalize(configuration["Legal:ShortName"]),
                Normalize(configuration["Legal:LegalForm"]),
                addressLines,
                email,
                legacyPhone,
                phoneNumbers,
                ReadList("Legal:RepresentedBy"),
                ReadList("Legal:ContentResponsible"),
                Normalize(configuration["Legal:ContentResponsibleRole"]),
                ReadList("Legal:ContentResponsibleAddressLines"),
                Normalize(configuration["Legal:Website"]),
                Normalize(configuration["Legal:RegisterCourt"]),
                Normalize(configuration["Legal:RegisterNumber"]),
                Normalize(configuration["Legal:VatId"]),
                Normalize(configuration["Legal:PrivacyContact"]))));
    }

    private string[] ReadList(string key) =>
        NormalizeList(configuration.GetSection(key).Get<string[]>() ?? Array.Empty<string>());

    private static string[] NormalizeList(IEnumerable<string> values) =>
        values.Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
