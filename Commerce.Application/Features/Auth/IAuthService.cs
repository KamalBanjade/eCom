using Commerce.Application.Features.Auth.DTOs;

namespace Commerce.Application.Features.Auth;

/// <summary>
/// Authentication service contract
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    // Password Reset
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
    
    // Multi-Factor Authentication
    Task<EnableMfaResponse> EnableMfaAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> VerifyAndEnableMfaAsync(string userId, string totpCode, CancellationToken cancellationToken = default);
    Task DisableMfaAsync(string userId, CancellationToken cancellationToken = default);
}
