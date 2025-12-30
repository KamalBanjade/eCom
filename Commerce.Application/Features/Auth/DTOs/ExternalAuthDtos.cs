using System.Text.Json.Serialization;

namespace Commerce.Application.Features.Auth.DTOs;

/// <summary>
/// Response for external OAuth login (Google, etc.)
/// </summary>
public class ExternalLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsNewUser { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Google user information from OAuth
/// </summary>
public class GoogleUserInfo
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty; // Google user ID
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonPropertyName("verified_email")]  // ✅ This is the key fix!
    public bool Email_Verified { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("given_name")]
    public string Given_Name { get; set; } = string.Empty;
    
    [JsonPropertyName("family_name")]
    public string Family_Name { get; set; } = string.Empty;
    
    [JsonPropertyName("picture")]
    public string Picture { get; set; } = string.Empty;
}

/// <summary>
/// Google token response from OAuth
/// </summary>
public class GoogleTokenResponse
{
    [JsonPropertyName("access_token")]
    public string Access_Token { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_in")]
    public int Expires_In { get; set; }
    
    [JsonPropertyName("token_type")]
    public string Token_Type { get; set; } = string.Empty;
    
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
    
    [JsonPropertyName("id_token")]
    public string Id_Token { get; set; } = string.Empty;
    
    [JsonPropertyName("refresh_token")]  // ✅ Added in case you need it later
    public string? Refresh_Token { get; set; }
}

/// <summary>
/// Decoded Google ID token payload
/// </summary>
public class GoogleIdTokenPayload
{
    public string Iss { get; set; } = string.Empty; // Issuer
    public string Sub { get; set; } = string.Empty; // Subject (user ID)
    public string Azp { get; set; } = string.Empty; // Authorized party
    public string Aud { get; set; } = string.Empty; // Audience
    public long Iat { get; set; } // Issued at
    public long Exp { get; set; } // Expiration
    public string Email { get; set; } = string.Empty;
    public bool Email_Verified { get; set; }
}