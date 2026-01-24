using Commerce.Domain.Entities.Users;
using Commerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Commerce.Infrastructure.Data.Configurations;

public class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.ApplicationUserId)
            .IsRequired();

        // Store addresses as JSON arrays
        builder.Property(c => c.ShippingAddresses)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Address>>(v, (JsonSerializerOptions?)null) ?? new List<Address>()
            );

        builder.Property(c => c.BillingAddresses)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Address>>(v, (JsonSerializerOptions?)null) ?? new List<Address>()
            );

        // Indexes
        builder.HasIndex(c => c.Email);
        builder.HasIndex(c => c.ApplicationUserId).IsUnique();
    }
}
