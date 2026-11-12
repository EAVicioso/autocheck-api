// AutoCheck academic prototype. No shared database is changed by migration generation.
using Microsoft.EntityFrameworkCore.Design;

namespace AutoCheck.Api.Infrastructure.Data;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AutoCheckDb>
{
    public AutoCheckDb CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var directory = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "AutoCheck.Api")) ? Path.Combine(Directory.GetCurrentDirectory(), "AutoCheck.Api") : Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder().SetBasePath(directory).AddJsonFile("appsettings.json", optional: true).AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (environment == "Development")
        {
            configuration.AddUserSecrets<Program>(optional: true);
        }

        configuration.AddEnvironmentVariables().AddCommandLine(args);
        return new(new DbContextOptionsBuilder<AutoCheckDb>().UseNpgsql(PostgresConnection.Resolve(configuration.Build(), migrations: true)).Options);
    }
}
