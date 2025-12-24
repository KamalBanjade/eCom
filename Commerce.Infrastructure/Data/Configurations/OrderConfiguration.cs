using Commerce.Domain.Entities.Orders;
using Commerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Commerce.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.SubTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.TaxAmount)
            .HasPrecision(18, 2);

        builder.Property(o => o.ShippingAmount)
            .HasPrecision(18, 2);

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        // Store addresses as JSON (PostgreSQL JSONB)
        builder.Property(o => o.ShippingAddress)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Address>(v, (JsonSerializerOptions?)null)!
            );

        builder.Property(o => o.BillingAddress)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Address>(v, (JsonSerializerOptions?)null)!
            );

        // Indexes
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.CustomerProfileId);
        builder.HasIndex(o => o.OrderStatus);
        builder.HasIndex(o => o.CreatedAt);

        // Relationships
        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.CustomerProfile)
            .WithMany()
            .HasForeignKey(o => o.CustomerProfileId);
    }
}
