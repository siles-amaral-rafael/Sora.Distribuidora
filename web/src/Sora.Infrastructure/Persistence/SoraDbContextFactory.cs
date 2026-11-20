using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace Sora.Infrastructure.Persistence;

public sealed class SoraDbContextFactory : IDesignTimeDbContextFactory<SoraDbContext>
{
    public SoraDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? ReadDevelopmentSecret()
            ?? "Host=localhost;Port=5432;Database=sora;Username=sora;Password=design-time-only;SSL Mode=Disable";

        var options = new DbContextOptionsBuilder<SoraDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SoraDbContext(options);
    }

    private static string? ReadDevelopmentSecret()
    {
        var secretsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".microsoft",
            "usersecrets",
            "sora-revision4-development",
            "secrets.json");

        if (!File.Exists(secretsFile))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(secretsFile));
        if (document.RootElement.TryGetProperty("ConnectionStrings:Postgres", out var flatValue))
        {
            return flatValue.GetString();
        }

        if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
            && connectionStrings.TryGetProperty("Postgres", out var nestedValue))
        {
            return nestedValue.GetString();
        }

        return null;
    }
}
