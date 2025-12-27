using Commerce.Domain.Entities.Carts;
using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Inventory;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Entities.Products;
using Commerce.Domain.Entities.Users;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Entities.Payments;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Data;

public class CommerceDbContext : IdentityDbContext<ApplicationUser>
{
    public CommerceDbContext(DbContextOptions<CommerceDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<PaymentAuditLog> PaymentAuditLogs => Set<PaymentAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);

        // Additional manual configurations (if needed beyond fluent configs)
        modelBuilder.Entity<StockReservation>()
            .HasIndex(r => r.ExpiresAt);

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.Property(e => e.CustomerProfileId).IsRequired(false);
            entity.Property(e => e.AnonymousId).HasMaxLength(100);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Cart_HasIdentifier",
                "\"CustomerProfileId\" IS NOT NULL OR \"AnonymousId\" IS NOT NULL"
            ));
        });

        // Order: Unique index on Pidx for Khalti payments
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.Pidx)
            .IsUnique()
            .HasFilter("\"Pidx\" IS NOT NULL");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is BaseEntity &&  // Now BaseEntity is recognized
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;  // Now this casts correctly

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}