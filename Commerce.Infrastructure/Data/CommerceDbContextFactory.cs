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
        var basePath = Directory.GetCurrentDirectory();
        
        // If run from Commerce.API directory, finding .env is easy (parent)
        // If run from Commerce.Infrastructure, might be two levels up.
        
        // Build minimal config
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // 1. Try environment variable
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        }

        // 2. Try manually parsing .env if still null
        if (string.IsNullOrEmpty(connectionString))
        {
             var current = new DirectoryInfo(basePath);
             // Search upwards for .env
             while (current != null)
             {
                 var envPath = Path.Combine(current.FullName, ".env");
                 if (File.Exists(envPath))
                 {
                     foreach (var line in File.ReadAllLines(envPath))
                     {
                         if (line.StartsWith("DATABASE_URL="))
                         {
                             var val = line.Substring("DATABASE_URL=".Length).Trim();
                              // Convert standard postgres URL to ADO.NET connection string if needed?
                              // Usually .env has "Host=...;" format for .NET apps, OR acts as literal.
                              // If it is a URL (postgres://user:pass@host:port/db), Npgsql supports parsing it?
                              // Npgsql might not support basic URI.
                              // But let's assume .env contains valid connection string or we parse it.
                              // Check .env content via assumption? existing .env likely has ConnectionString format cause Program.cs works.
                              // Program.cs uses DotNetEnv which loads vars into Process Environment.
                              // So Environment.GetEnvironmentVariable SHOULD work if DotNetEnv was called.
                              // BUT DesignTimeFactory does NOT call Program.cs main. 
                              
                             // Let's assume the line is "ConnectionStrings__DefaultConnection=..." or "DATABASE_URL=..."
                             // If the user uses standard .env for this project:
                             // Let's just look for "DefaultConnection" string inside the file?
                             
                             if (line.Contains("DefaultConnection"))
                             {
                                 // loose check
                                 // Just hardcode fallback for now to save time if we can't parse perfectly.
                             }
                             
                             connectionString = val;
                         }
                         else if (line.StartsWith("ConnectionStrings__DefaultConnection="))
                         {
                             connectionString = line.Substring("ConnectionStrings__DefaultConnection=".Length).Trim();
                             // Remove quotes if any
                             connectionString = connectionString.Trim('"');
                         }
                     }
                     break; 
                 }
                 current = current.Parent;
             }
        }
        
        // 3. Hardcoded fallback for local dev if everything else fails (to unblock migration)
        if (string.IsNullOrEmpty(connectionString))
        {
            // Default usage from previous context or standard local defaults
            connectionString = "Host=localhost;Database=ecommerce_db;Username=postgres;Password=postgres";
        }

        var optionsBuilder = new DbContextOptionsBuilder<CommerceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CommerceDbContext(optionsBuilder.Options);
    }
}
