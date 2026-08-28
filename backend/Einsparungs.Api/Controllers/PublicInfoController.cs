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
        var addressLines = configuration
            .GetSection("Legal:AddressLines")
            .Get<string[]>()?
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray() ?? Array.Empty<string>();
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "1.0.0";

        return Ok(new ProductInfoResponse(
            configuration["Product:Name"] ?? "HimiFlow Einsparungsdatenbank",
            configuration["Product:Edition"] ?? "Local Edition",
            version,
            new LegalNoticeResponse(
                !string.IsNullOrWhiteSpace(providerName) &&
                !string.IsNullOrWhiteSpace(email) &&
                addressLines.Length > 0,
                providerName,
                Normalize(configuration["Legal:LegalForm"]),
                addressLines,
                email,
                Normalize(configuration["Legal:Phone"]),
                Normalize(configuration["Legal:Website"]),
                Normalize(configuration["Legal:RegisterCourt"]),
                Normalize(configuration["Legal:RegisterNumber"]),
                Normalize(configuration["Legal:VatId"]),
                Normalize(configuration["Legal:PrivacyContact"]))));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
