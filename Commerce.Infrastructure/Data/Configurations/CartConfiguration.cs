using Commerce.Domain.Entities.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Data.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.HasKey(c => c.Id);

        // Configure nullable foreign keys for identity constraint
        builder.Property(c => c.CustomerProfileId)
            .IsRequired(false);

        builder.Property(c => c.AnonymousId)
            .HasMaxLength(100)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(c => c.CustomerProfileId);
        builder.HasIndex(c => c.AnonymousId);
        builder.HasIndex(c => c.ExpiresAt);

        // Relationships
        builder.HasMany(c => c.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
           .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CustomerProfile)
            .WithMany()
            .HasForeignKey(c => c.CustomerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
