namespace Commerce.Infrastructure.Identity;

/// <summary>
/// Password reset token entity with security constraints
/// CRITICAL: Token is stored as SHA-256 hash, NEVER plain text
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // SHA-256 hash of the token (NEVER store plain text)
    public string TokenHash { get; set; } = string.Empty;
    
    // Email for lookup
    public string Email { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15); // 15-minute expiration
    
    // For single-use enforcement
    public DateTime? UsedAt { get; set; }
    
    // Validation logic
    public bool IsValid => UsedAt == null && DateTime.UtcNow < ExpiresAt;
}
