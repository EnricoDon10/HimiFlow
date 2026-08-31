using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Einsparungs.Api.Security;

public sealed class ProductionConfigurationOptions
{
    public bool IsProduction { get; set; }
    public string Provider { get; set; } = "SQLite";
    public string ConnectionString { get; set; } = string.Empty;
    public string? DatabasePath { get; set; }
    public string BackupDirectory { get; set; } = string.Empty;
    public string? DataProtectionKeyRingPath { get; set; }
    public bool LicenseEnforcementEnabled { get; set; }
    public string? LicenseInstallationId { get; set; }
    public string? LicensePublicKeyPem { get; set; }
    public bool SeedOnStartup { get; set; }
    public bool SeedDemoReferenceData { get; set; }
    public bool RequireHttps { get; set; }
    public int BackupIntervalHours { get; set; }
    public int BackupMaximumAgeHours { get; set; }
    public int BackupRetentionDays { get; set; }
    public int BackupMinimumBackupsToKeep { get; set; }

    public void Apply(ProductionConfigurationOptions source)
    {
        IsProduction = source.IsProduction;
        Provider = source.Provider;
        ConnectionString = source.ConnectionString;
        DatabasePath = source.DatabasePath;
        BackupDirectory = source.BackupDirectory;
        DataProtectionKeyRingPath = source.DataProtectionKeyRingPath;
        LicenseEnforcementEnabled = source.LicenseEnforcementEnabled;
        LicenseInstallationId = source.LicenseInstallationId;
        LicensePublicKeyPem = source.LicensePublicKeyPem;
        SeedOnStartup = source.SeedOnStartup;
        SeedDemoReferenceData = source.SeedDemoReferenceData;
        RequireHttps = source.RequireHttps;
        BackupIntervalHours = source.BackupIntervalHours;
        BackupMaximumAgeHours = source.BackupMaximumAgeHours;
        BackupRetentionDays = source.BackupRetentionDays;
        BackupMinimumBackupsToKeep = source.BackupMinimumBackupsToKeep;
    }

    public static ProductionConfigurationOptions From(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        string? databasePath = null;

        if (string.Equals(configuration["Database:Provider"] ?? "SQLite", "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                databasePath = new SqliteConnectionStringBuilder(connectionString).DataSource;
            }
            catch (ArgumentException)
            {
                // The validator emits the actionable configuration error.
            }
        }

        return new ProductionConfigurationOptions
        {
            IsProduction = environment.IsProduction(),
            Provider = configuration["Database:Provider"]?.Trim() ?? "SQLite",
            ConnectionString = connectionString,
            DatabasePath = databasePath,
            BackupDirectory = configuration["Backup:Directory"]?.Trim() ?? "backups",
            DataProtectionKeyRingPath = configuration["DataProtection:KeyRingPath"]?.Trim(),
            LicenseEnforcementEnabled = configuration.GetValue<bool?>("License:EnforcementEnabled") ?? !environment.IsDevelopment(),
            LicenseInstallationId = configuration["License:InstallationId"]?.Trim(),
            LicensePublicKeyPem = configuration["License:PublicKeyPem"],
            SeedOnStartup = configuration.GetValue("Database:SeedOnStartup", environment.IsDevelopment()),
            SeedDemoReferenceData = configuration.GetValue("Database:SeedDemoReferenceData", environment.IsDevelopment()),
            RequireHttps = configuration.GetValue("Security:RequireHttps", !environment.IsDevelopment()),
            BackupIntervalHours = configuration.GetValue("Backup:IntervalHours", 24),
            BackupMaximumAgeHours = configuration.GetValue("Backup:MaximumAgeHours", 36),
            BackupRetentionDays = configuration.GetValue("Backup:RetentionDays", 30),
            BackupMinimumBackupsToKeep = configuration.GetValue("Backup:MinimumBackupsToKeep", 7)
        };
    }
}

public sealed class ProductionConfigurationValidator : IValidateOptions<ProductionConfigurationOptions>
{
    private readonly IHostEnvironment environment;

    public ProductionConfigurationValidator(IHostEnvironment environment)
    {
        this.environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, ProductionConfigurationOptions options)
    {
        if (!options.IsProduction && !environment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        var webRoot = (environment as IWebHostEnvironment)?.WebRootPath;

        if (!options.RequireHttps)
        {
            errors.Add("Security:RequireHttps muss in Production aktiviert sein.");
        }

        if (options.SeedOnStartup || options.SeedDemoReferenceData)
        {
            errors.Add("Demo-/Seed-Daten dürfen in Production nicht automatisch erzeugt werden.");
        }

        if (string.IsNullOrWhiteSpace(options.DataProtectionKeyRingPath))
        {
            errors.Add("DataProtection:KeyRingPath muss in Production konfiguriert sein.");
        }
        else
        {
            ValidateDirectory(options.DataProtectionKeyRingPath, "DataProtection-Key-Ring", webRoot, errors);
        }

        if (string.Equals(options.Provider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.DatabasePath) ||
                string.Equals(options.DatabasePath, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Für SQLite in Production ist eine dateibasierte Datenbank erforderlich.");
            }
            else
            {
                var databasePath = Path.GetFullPath(options.DatabasePath, environment.ContentRootPath);
                if (IsWithinDirectory(databasePath, webRoot))
                {
                    errors.Add("SQLite-Datenbank darf nicht unterhalb von wwwroot liegen.");
                }

                var databaseDirectory = Path.GetDirectoryName(databasePath);
                if (string.IsNullOrWhiteSpace(databaseDirectory))
                {
                    errors.Add("Das Verzeichnis der SQLite-Datenbank konnte nicht bestimmt werden.");
                }
                else
                {
                    ValidateDirectory(databaseDirectory, "SQLite-Datenbankverzeichnis", webRoot, errors);
                }
            }
        }
        else if (string.Equals(options.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                errors.Add("ConnectionStrings:DefaultConnection muss für SQL Server konfiguriert sein.");
            }
        }
        else
        {
            errors.Add($"Database:Provider '{options.Provider}' wird nicht unterstützt.");
        }

        if (string.IsNullOrWhiteSpace(options.BackupDirectory))
        {
            errors.Add("Backup:Directory muss konfiguriert sein.");
        }
        else
        {
            var backupDirectory = Path.GetFullPath(options.BackupDirectory, environment.ContentRootPath);
            if (IsWithinDirectory(backupDirectory, webRoot))
            {
                errors.Add("Backup-Verzeichnis darf nicht unterhalb von wwwroot liegen.");
            }

            if (options.DatabasePath is not null &&
                string.Equals(backupDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(options.DatabasePath, environment.ContentRootPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Backup-Verzeichnis darf nicht identisch mit der SQLite-Datenbankdatei sein.");
            }

            ValidateDirectory(backupDirectory, "Backup-Verzeichnis", webRoot, errors);
        }

        if (options.BackupIntervalHours is < 1 or > 168)
        {
            errors.Add("Backup:IntervalHours muss zwischen 1 und 168 liegen.");
        }

        if (options.BackupMaximumAgeHours < options.BackupIntervalHours || options.BackupMaximumAgeHours > 720)
        {
            errors.Add("Backup:MaximumAgeHours muss mindestens dem Intervall entsprechen und darf höchstens 720 betragen.");
        }

        if (options.BackupRetentionDays is < 1 or > 3650)
        {
            errors.Add("Backup:RetentionDays muss zwischen 1 und 3650 liegen.");
        }

        if (options.BackupMinimumBackupsToKeep is < 1 or > 10000)
        {
            errors.Add("Backup:MinimumBackupsToKeep muss zwischen 1 und 10000 liegen.");
        }

        if (options.LicenseEnforcementEnabled)
        {
            if (string.IsNullOrWhiteSpace(options.LicenseInstallationId))
            {
                errors.Add("License:InstallationId muss bei aktiver Lizenzdurchsetzung konfiguriert sein.");
            }

            if (options.LicensePublicKeyPem?.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase) == true)
            {
                errors.Add("License:PublicKeyPem darf keinen privaten Signaturschlüssel enthalten.");
            }
            else if (string.IsNullOrWhiteSpace(options.LicensePublicKeyPem) ||
                     !options.LicensePublicKeyPem.Contains("PUBLIC KEY", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("License:PublicKeyPem muss bei aktiver Lizenzdurchsetzung einen gültigen Public-Key enthalten.");
            }
        }

        if (errors.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "HimiFlow kann nicht gestartet werden:\n- " + string.Join("\n- ", errors));
    }

    private void ValidateDirectory(string configuredPath, string label, string? webRoot, ICollection<string> errors)
    {
        try
        {
            var fullPath = Path.GetFullPath(configuredPath, environment.ContentRootPath);
            if (IsWithinDirectory(fullPath, webRoot))
            {
                errors.Add($"{label} darf nicht unterhalb von wwwroot liegen.");
                return;
            }

            Directory.CreateDirectory(fullPath);
            var probe = Path.Combine(fullPath, $".himiflow-write-test-{Guid.NewGuid():N}");
            using (File.Create(probe)) { }
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            errors.Add($"{label} ist nicht erreichbar oder nicht beschreibbar.");
        }
    }

    private static bool IsWithinDirectory(string candidatePath, string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        var candidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
        var directory = EnsureTrailingSeparator(Path.GetFullPath(directoryPath));
        return candidate.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
