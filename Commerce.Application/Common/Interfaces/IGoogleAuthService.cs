using Commerce.Application.Features.Auth.DTOs;

namespace Commerce.Application.Common.Interfaces;

/// <summary>
/// Service for handling Google OAuth authentication
/// </summary>
public interface IGoogleAuthService
{
    /// <summary>
    /// Generates Google OAuth login URL with state parameter for CSRF protection
    /// </summary>
    /// <param name="state">Output parameter containing the generated state value</param>
    /// <returns>Google authorization URL</returns>
    string GetGoogleLoginUrl(out string state);
    
    /// <summary>
    /// Handles Google OAuth callback, validates tokens, and creates/logs in user
    /// </summary>
    /// <param name="authorizationCode">Authorization code from Google</param>
    /// <param name="state">State parameter for CSRF validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT tokens and user information</returns>
    Task<ExternalLoginResponse> HandleGoogleCallbackAsync(
        string authorizationCode,
        string state,
        CancellationToken cancellationToken = default);
}
