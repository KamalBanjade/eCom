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

        builder.Property(v => v.DiscountPrice)
               .HasPrecision(18, 2);

        builder.Property(v => v.StockQuantity)
               .HasColumnName("AvailableStock")   // This fixes the mismatch!
               .HasColumnType("integer")
               .HasDefaultValue(0)
               .IsRequired();

        builder.Property(v => v.IsActive)
               .IsRequired();

        builder.Property(v => v.ImageUrls)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                   v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) 
                        ?? new List<string>()
               )
               .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                   (c1, c2) => c1.SequenceEqual(c2),
                   c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                   c => c.ToList()))
               ;
               
        builder.Property(v => v.ImageUrls).IsRequired();

        // Store attributes as JSONB in PostgreSQL
        builder.Property(v => v.Attributes)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                   v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default) 
                        ?? new Dictionary<string, string>()
               )
               .IsRequired();

        builder.Property(v => v.ProductId)
               .IsRequired();

        // Indexes
        builder.HasIndex(v => v.ProductId);
        builder.HasIndex(v => v.SKU)
               .IsUnique();

        builder.ToTable("ProductVariants");
    }
}