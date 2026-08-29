using Einsparungs.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Einsparungs.Api.Tests;

[TestClass]
public sealed class DataProtectionConfigurationTests
{
    [TestMethod]
    public void ProductionWithoutKeyRingPathFailsClosed()
    {
        var services = new ServiceCollection();
        var environment = CreateEnvironment(Environments.Production);
        var configuration = new ConfigurationBuilder().Build();

        var threw = false;
        try { DataProtectionConfiguration.Configure(services, configuration, environment); }
        catch (InvalidOperationException) { threw = true; }
        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void ConfiguredKeyRingIsAcceptedOutsideWebRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-dp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeyRingPath"] = "keys"
                })
                .Build();
            var environment = CreateEnvironment(Environments.Production, root);

            DataProtectionConfiguration.Configure(services, configuration, environment);

            Assert.IsTrue(Directory.Exists(Path.Combine(root, "keys")));
            Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType.Name.Contains("DataProtection", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void KeyRingUnderWebRootIsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HimiFlow-dp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        try
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeyRingPath"] = Path.Combine(root, "wwwroot", "keys")
                })
                .Build();
            var environment = CreateEnvironment(Environments.Production, root);
            var threw = false;
            try { DataProtectionConfiguration.Configure(services, configuration, environment); }
            catch (InvalidOperationException) { threw = true; }
            Assert.IsTrue(threw);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static TestHostEnvironment CreateEnvironment(string name, string? root = null)
    {
        var contentRoot = root ?? Path.GetTempPath();
        return new TestHostEnvironment(contentRoot)
        {
            EnvironmentName = name,
            WebRootPath = Path.Combine(contentRoot, "wwwroot"),
            WebRootFileProvider = new NullFileProvider()
        };
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment, IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HimiFlow.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
