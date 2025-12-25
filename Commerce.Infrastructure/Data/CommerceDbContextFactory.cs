using Commerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Commerce.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core migrations
/// </summary>
public class CommerceDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Commerce.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Build DbContextOptions
        var optionsBuilder = new DbContextOptionsBuilder<CommerceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CommerceDbContext(optionsBuilder.Options);
    }
}
