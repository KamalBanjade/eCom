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
               .HasDefaultValue(true)
               .IsRequired();

        builder.Property(v => v.ImageUrl)
               .HasColumnType("text");

        // Store attributes as JSONB in PostgreSQL
        builder.Property(v => v.Attributes)
               .HasColumnType("jsonb")
                .IsRequired()
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default) 
                         ?? new Dictionary<string, string>()
                )
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, JsonSerializerOptions.Default) == JsonSerializer.Serialize(c2, JsonSerializerOptions.Default),
                    c => c == null ? 0 : JsonSerializer.Serialize(c, JsonSerializerOptions.Default).GetHashCode(),
                    c => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(c, JsonSerializerOptions.Default), JsonSerializerOptions.Default) ?? new Dictionary<string, string>()));

        builder.Property(v => v.ProductId)
               .IsRequired();

        // Indexes
        builder.HasIndex(v => v.ProductId);
        builder.HasIndex(v => v.SKU)
               .IsUnique();

        builder.ToTable("ProductVariants");
    }
}