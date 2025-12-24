using Microsoft.AspNetCore.Identity;

namespace Commerce.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Navigation to business domain
    public Guid? CustomerProfileId { get; set; }
    
    // Refresh tokens collection
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
