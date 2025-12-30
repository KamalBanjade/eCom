using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth.DTOs;
using Commerce.Domain.Entities.Users;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CommerceDbContext _context;
    
    private const string GoogleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
    private const string GoogleUserInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo";
    private const string GoogleIssuer = "https://accounts.google.com";
    
    public GoogleAuthService(
        HttpClient httpClient,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        CommerceDbContext context)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _userManager = userManager;
        _context = context;
    }

    public string GetGoogleLoginUrl(out string state)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            throw new InvalidOperationException("Google ClientId not configured");

        // Generate cryptographically secure state parameter for CSRF protection
        state = GenerateSecureState();
        
        var redirectUri = _configuration["Authentication:Google:RedirectUri"] 
            ?? $"{_configuration["BackendUrl"]}/api/auth/google/callback";
        var scope = "openid email profile";
        
        var queryParams = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "redirect_uri", redirectUri },
            { "response_type", "code" },
            { "scope", scope },
            { "state", state },
            { "access_type", "offline" },
            { "prompt", "consent" }
        };
        
        var queryString = string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        
        return $"{GoogleAuthUrl}?{queryString}";
    }

    public async Task<ExternalLoginResponse> HandleGoogleCallbackAsync(
        string authorizationCode,
        string state,
        CancellationToken cancellationToken = default)
    {
        // 1. Exchange authorization code for tokens
        var tokenResponse = await ExchangeCodeForTokensAsync(authorizationCode, cancellationToken);
        
        // 2. Validate and decode ID token
        var idTokenPayload = ValidateAndDecodeIdToken(tokenResponse.Id_Token);
        
        // 3. Fetch user info from Google
        var userInfo = await GetGoogleUserInfoAsync(tokenResponse.Access_Token, cancellationToken);
        
        // 4. Validate user info
        ValidateUserInfo(userInfo);
        
        // 5. Find or create user (Customer role only)
        var (user, isNewUser) = await FindOrCreateUserAsync(userInfo, idTokenPayload.Sub, cancellationToken);
        
        // 6. Generate JWT tokens
        return await GenerateAuthResponseAsync(user, isNewUser);
    }

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(
        string authorizationCode,
        CancellationToken cancellationToken)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        var clientSecret = _configuration["Authentication:Google:ClientSecret"];
        
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("Google OAuth credentials not configured");

        var requestData = new Dictionary<string, string>
        {
            { "code", authorizationCode },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "redirect_uri", _configuration["Authentication:Google:RedirectUri"] 
                ?? $"{_configuration["BackendUrl"]}/api/auth/google/callback" },
            { "grant_type", "authorization_code" }
        };

        var response = await _httpClient.PostAsync(
            GoogleTokenUrl,
            new FormUrlEncodedContent(requestData),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to exchange authorization code: {error}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.Id_Token))
            throw new InvalidOperationException("Invalid token response from Google");

        return tokenResponse;
    }

    private GoogleIdTokenPayload ValidateAndDecodeIdToken(string idToken)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        
        // Decode JWT without validation first to get payload
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(idToken);
        
        // Extract payload
        var payload = new GoogleIdTokenPayload
        {
            Iss = jwtToken.Claims.FirstOrDefault(c => c.Type == "iss")?.Value ?? "",
            Sub = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "",
            Aud = jwtToken.Claims.FirstOrDefault(c => c.Type == "aud")?.Value ?? "",
            Azp = jwtToken.Claims.FirstOrDefault(c => c.Type == "azp")?.Value ?? "",
            Email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "",
            Email_Verified = bool.Parse(jwtToken.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value ?? "false"),
            Iat = long.Parse(jwtToken.Claims.FirstOrDefault(c => c.Type == "iat")?.Value ?? "0"),
            Exp = long.Parse(jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value ?? "0")
        };
        
        // CRITICAL SECURITY VALIDATIONS
        
        // 1. Validate issuer
        if (payload.Iss != GoogleIssuer && payload.Iss != "accounts.google.com")
        {
            throw new SecurityException($"Invalid token issuer: {payload.Iss}");
        }
        
        // 2. Validate audience (must match our ClientId)
        if (payload.Aud != clientId)
        {
            throw new SecurityException($"Invalid token audience: {payload.Aud}");
        }
        
        // 3. Validate expiration
        var expirationTime = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
        if (expirationTime < DateTimeOffset.UtcNow)
        {
            throw new SecurityException("Token has expired");
        }
        
        // 4. Validate issued at time (not too old, not in future)
        var issuedAtTime = DateTimeOffset.FromUnixTimeSeconds(payload.Iat);
        if (issuedAtTime > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new SecurityException("Token issued in the future");
        }
        
        return payload;
    }

    private async Task<GoogleUserInfo> GetGoogleUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync(GoogleUserInfoUrl, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to fetch user info from Google");
        }

        var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfo>(cancellationToken);
        
        if (userInfo == null)
            throw new InvalidOperationException("Invalid user info response from Google");

        return userInfo;
    }

    private void ValidateUserInfo(GoogleUserInfo userInfo)
    {
        // Validate email exists
        if (string.IsNullOrEmpty(userInfo.Email))
        {
            throw new InvalidOperationException("Email not provided by Google");
        }
        // Validate email is verified
        if (!userInfo.Email_Verified)
        {
            throw new InvalidOperationException("Email not verified by Google");
        }
    }

    private async Task<(ApplicationUser user, bool isNewUser)> FindOrCreateUserAsync(
        GoogleUserInfo userInfo,
        string googleId,
        CancellationToken cancellationToken)
    {
        // Try to find existing user by GoogleId
        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.GoogleId == googleId, cancellationToken);

        if (existingUser != null)
        {
            // Existing OAuth user
            return (existingUser, false);
        }

        // Check if email already exists (user might have registered with password)
        var userByEmail = await _userManager.FindByEmailAsync(userInfo.Email);
        if (userByEmail != null)
        {
            // Email exists but not linked to Google - could link here or throw error
            // For security, we'll throw an error to prevent account takeover
            throw new InvalidOperationException(
                "An account with this email already exists. Please login with your password.");
        }

        // Create new user
        var newUser = new ApplicationUser
        {
            UserName = userInfo.Email,
            Email = userInfo.Email,
            EmailConfirmed = true, // Google verified the email
            GoogleId = googleId,
            Provider = "Google"
            // No password set for OAuth users
        };

        var result = await _userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // CRITICAL: Only assign Customer role (never Admin)
        await _userManager.AddToRoleAsync(newUser, "Customer");

        // Create CustomerProfile
        var customerProfile = new CustomerProfile
        {
            FirstName = userInfo.Given_Name ?? "",
            LastName = userInfo.Family_Name ?? "",
            Email = userInfo.Email,
            ApplicationUserId = newUser.Id
        };

        _context.CustomerProfiles.Add(customerProfile);
        await _context.SaveChangesAsync(cancellationToken);

        // Link user to profile
        newUser.CustomerProfileId = customerProfile.Id;
        await _userManager.UpdateAsync(newUser);

        return (newUser, true);
    }

    private async Task<ExternalLoginResponse> GenerateAuthResponseAsync(
        ApplicationUser user,
        bool isNewUser)
    {
        var roles = await _userManager.GetRolesAsync(user);
        
        // Generate JWT access token
        var accessToken = GenerateAccessToken(user, roles.ToList());
        
        // Generate refresh token
        var refreshToken = GenerateRefreshToken();
        
        // Store refresh token (hashed)
        var tokenHash = HashToken(refreshToken);
        var refreshTokenEntity = new RefreshToken
        {
            TokenHash = tokenHash,
            ApplicationUserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return new ExternalLoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email!,
            Role = roles.FirstOrDefault() ?? "Customer",
            IsNewUser = isNewUser,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        };
    }

    private string GenerateAccessToken(ApplicationUser user, List<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add roles as claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }

    private static string GenerateSecureState()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
