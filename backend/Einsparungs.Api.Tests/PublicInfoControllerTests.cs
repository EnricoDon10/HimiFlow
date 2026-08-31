using Einsparungs.Api.Controllers;
using Einsparungs.Api.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class PublicInfoControllerTests
{
    [TestMethod]
    public void ProductInfo_ExposesConfiguredProviderAndHidesEmptyVatId()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Product:Name"] = "HimiFlow Einsparungsdatenbank",
                ["Product:Edition"] = "Local Edition",
                ["Legal:ProviderName"] = "ME Digitale GbR Dirr & Mancuso",
                ["Legal:ShortName"] = "ME Digitale",
                ["Legal:AddressLines:0"] = "Fersenbruch 68",
                ["Legal:AddressLines:1"] = "45883 Gelsenkirchen",
                ["Legal:AddressLines:2"] = "Deutschland",
                ["Legal:Email"] = "info@medigitale.de",
                ["Legal:PhoneNumbers:0"] = "+49 176 64764025",
                ["Legal:PhoneNumbers:1"] = "+49 151 68488353",
                ["Legal:RepresentedBy:0"] = "Enrico Mancuso",
                ["Legal:RepresentedBy:1"] = "Maximilian Dirr",
                ["Legal:ContentResponsible:0"] = "Enrico Mancuso",
                ["Legal:ContentResponsible:1"] = "Maximilian Dirr",
                ["Legal:ContentResponsibleRole"] = "Gesellschafter der ME Digitale GbR Dirr & Mancuso",
                ["Legal:ContentResponsibleAddressLines:0"] = "Fersenbruch 68",
                ["Legal:ContentResponsibleAddressLines:1"] = "45883 Gelsenkirchen"
            })
            .Build();

        var result = new PublicInfoController(configuration).GetProductInfo();
        var response = ((Microsoft.AspNetCore.Mvc.OkObjectResult)result.Result!).Value as ProductInfoResponse;

        Assert.IsNotNull(response);
        Assert.IsTrue(response.LegalNotice.IsConfigured);
        Assert.AreEqual("0.9.0-rc.1", response.Version);
        Assert.AreEqual("ME Digitale", response.LegalNotice.ShortName);
        CollectionAssert.AreEqual(new[] { "+49 176 64764025", "+49 151 68488353" }, response.LegalNotice.PhoneNumbers.ToArray());
        CollectionAssert.AreEqual(new[] { "Enrico Mancuso", "Maximilian Dirr" }, response.LegalNotice.RepresentedBy.ToArray());
        Assert.IsNull(response.LegalNotice.VatId);
        Assert.IsFalse(response.LegalNotice.AddressLines.Any(line => line.Contains("UST", StringComparison.OrdinalIgnoreCase)));
    }
}
