using Commerce.Domain.Entities.Carts;
using Commerce.Domain.Entities.Inventory;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Entities.Products;
using Commerce.Domain.Entities.Users;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Data;

public class CommerceDbContext : IdentityDbContext<ApplicationUser>
{
    public CommerceDbContext(DbContextOptions<CommerceDbContext> options) : base(options)
    {
    }

    // Domain entities
    public DbSet<CustomerProfile> CustomerProfiles { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    
    // Identity
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.Property(e => e.CustomerProfileId)
                .IsRequired(false); // Make nullable

            entity.Property(e => e.AnonymousId)
                .HasMaxLength(100);

            // Ensure either CustomerProfileId or AnonymousId exists
            // Ensure either CustomerProfileId or AnonymousId exists
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Cart_HasIdentifier",
                "\"CustomerProfileId\" IS NOT NULL OR \"AnonymousId\" IS NOT NULL"
            ));
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Commerce.Domain.Entities.Base.BaseEntity &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (Commerce.Domain.Entities.Base.BaseEntity)entry.Entity;
            
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }
            
            entity.UpdatedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
