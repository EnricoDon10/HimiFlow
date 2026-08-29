using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;

namespace Einsparungs.Api.Security;

/// <summary>
/// Configures the ASP.NET Core key ring without placing keys in the web root or source control.
/// </summary>
public static class DataProtectionConfiguration
{
    public static void Configure(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration["DataProtection:KeyRingPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    "DataProtection:KeyRingPath muss außerhalb von Development/Testing konfiguriert sein. " +
                    "Verwende einen nicht öffentlich ausgelieferten, persistenten Ordner mit restriktiven Dateirechten.");
            }

            services.AddDataProtection().SetApplicationName("HimiFlow");
            return;
        }

        var keyRingPath = Path.GetFullPath(configuredPath, environment.ContentRootPath);
        var webRootPath = (environment as IWebHostEnvironment)?.WebRootPath;
        if (!string.IsNullOrWhiteSpace(webRootPath) && IsWithinDirectory(keyRingPath, webRootPath))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyRingPath darf nicht unterhalb des öffentlich ausgelieferten WebRoot liegen.");
        }

        Directory.CreateDirectory(keyRingPath);
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("HimiFlow")
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

        // Windows DPAPI protects the persisted key-ring at rest without inventing custom cryptography.
        // On Linux/container deployments, protect the directory with the host/container secret store.
        if (OperatingSystem.IsWindows())
        {
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }
    }

    private static bool IsWithinDirectory(string candidatePath, string directoryPath)
    {
        var candidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
        var directory = EnsureTrailingSeparator(Path.GetFullPath(directoryPath));
        return candidate.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
