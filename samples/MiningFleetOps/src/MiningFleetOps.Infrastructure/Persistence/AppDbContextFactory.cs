using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MiningFleetOps.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseSqlServer(ResolveConnectionString());
        
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        return new AppDbContext(options.Options);
    }

    private static string ResolveConnectionString()
    {
        var environmentValue = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue;

        var appSettingsPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "MiningFleetOps.Api",
            "appsettings.json"));
        if (File.Exists(appSettingsPath))
        {
            using var stream = File.OpenRead(appSettingsPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
                connectionStrings.TryGetProperty("DefaultConnection", out var configured) &&
                configured.ValueKind == JsonValueKind.String)
            {
                var value = configured.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return "Data Source=localhost\\SQLEXPRESS;Initial Catalog=MiningFleetOpsDB;Integrated Security=True;Connect Timeout=5;Encrypt=False;Trust Server Certificate=True";
    }
}