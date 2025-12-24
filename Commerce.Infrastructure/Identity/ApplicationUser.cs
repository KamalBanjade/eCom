using Microsoft.AspNetCore.Identity;

namespace Commerce.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Navigation to business domain
    public Guid? CustomerProfileId { get; set; }
    
    // Refresh tokens collection
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    
    // Multi-Factor Authentication
    public bool MfaEnabled { get; set; } = false;
    public string? MfaSecret { get; set; } // TOTP secret (base32 encoded)
}
