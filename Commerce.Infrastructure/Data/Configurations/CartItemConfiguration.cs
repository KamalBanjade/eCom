using Commerce.Domain.Entities.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Data.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.PriceAtAdd)
            .HasPrecision(18, 2);

        // Indexes
        builder.HasIndex(i => i.CartId);
        builder.HasIndex(i => i.ProductVariantId);
    }
}
