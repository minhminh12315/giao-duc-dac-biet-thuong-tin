using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Secms.Infrastructure.Persistence;

public class SecmsDbContextFactory : IDesignTimeDbContextFactory<SecmsDbContext>
{
    public SecmsDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Secms.Api");
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var options = new DbContextOptionsBuilder<SecmsDbContext>()
            .UseNpgsql(config.GetConnectionString("DefaultConnection")
                        ?? "Host=localhost;Port=5432;Database=secms;Username=secms;Password=secms")
            .Options;
        return new SecmsDbContext(options);
    }
}
