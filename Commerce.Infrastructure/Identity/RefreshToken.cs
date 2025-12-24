namespace Commerce.Infrastructure.Identity;

/// <summary>
/// Refresh token entity with security constraints
/// CRITICAL: Token is stored as SHA-256 hash, NEVER plain text
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // SHA-256 hash of the token (NEVER store plain text)
    public string TokenHash { get; set; } = string.Empty;
    
    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser ApplicationUser { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    
    // For token rotation
    public string? ReplacedByTokenHash { get; set; }
    
    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
}
