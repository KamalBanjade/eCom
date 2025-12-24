using Commerce.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Commerce.Infrastructure.Data.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.SKU)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.Price)
            .HasPrecision(18, 2);

        // Store attributes as JSON (PostgreSQL JSONB)
        builder.Property(v => v.Attributes)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>()
            );

        // Indexes
        builder.HasIndex(v => v.SKU).IsUnique();
        builder.HasIndex(v => v.ProductId);
    }
}
