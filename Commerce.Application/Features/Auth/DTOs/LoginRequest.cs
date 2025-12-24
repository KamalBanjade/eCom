namespace Commerce.Application.Features.Auth.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    
    // Optional MFA code for two-step authentication
    public string? MfaCode { get; set; }
}
