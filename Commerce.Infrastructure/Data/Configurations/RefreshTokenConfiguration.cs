using Commerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(rt => rt.ApplicationUserId)
            .IsRequired();

        // Indexes
        builder.HasIndex(rt => rt.TokenHash);
        builder.HasIndex(rt => rt.ApplicationUserId);
        builder.HasIndex(rt => rt.ExpiresAt);

        // Relationship
        builder.HasOne(rt => rt.ApplicationUser)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
