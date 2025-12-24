using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Auth.DTOs;
using Commerce.Domain.Entities.Users;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Commerce.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly CommerceDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CommerceDbContext context,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Create ApplicationUser
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Assign default Customer role
        await _userManager.AddToRoleAsync(user, "Customer");

        // Create CustomerProfile
        var customerProfile = new CustomerProfile
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            ApplicationUserId = user.Id
        };

        _context.CustomerProfiles.Add(customerProfile);
        await _context.SaveChangesAsync(cancellationToken);

        // Link user to profile
        user.CustomerProfileId = customerProfile.Id;
        await _userManager.UpdateAsync(user);

        // Generate tokens
        return await GenerateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        return await GenerateAuthResponse(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        // Hash the incoming token
        var tokenHash = HashToken(request.RefreshToken);

        // Find the refresh token
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.ApplicationUser)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        // Get user
        var user = await _userManager.FindByIdAsync(refreshToken.ApplicationUserId);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Revoke old token (token rotation)
        refreshToken.RevokedAt = DateTime.UtcNow;
        
        // Generate new tokens
        var authResponse = await GenerateAuthResponse(user);
        
        // Set the replacement relationship
        var newTokenHash = HashToken(authResponse.RefreshToken);
        refreshToken.ReplacedByTokenHash = newTokenHash;
        
        await _context.SaveChangesAsync(cancellationToken);

        return authResponse;
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (token == null)
        {
            return false;
        }

        token.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<AuthResponse> GenerateAuthResponse(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles.ToList());
        var refreshToken = GenerateRefreshToken();

        // Store refresh token (HASHED)
        var tokenHash = HashToken(refreshToken);
        var refreshTokenEntity = new RefreshToken
        {
            TokenHash = tokenHash,
            ApplicationUserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // 7 days expiration
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken, // Return plain text to client
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            UserId = user.Id,
            Email = user.Email!,
            Roles = roles.ToList()
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

    /// <summary>
    /// Hash token using SHA-256 - CRITICAL for security
    /// </summary>
    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }
}
