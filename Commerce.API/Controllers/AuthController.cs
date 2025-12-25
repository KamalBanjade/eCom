using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Registers a new user with 'Customer' role
    /// </summary>
    /// <param name="request">Registration details including email, password, and profile info</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auth response with tokens</returns>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            result,
            "Registration successful"
        ));
    }

    /// <summary>
    /// Authenticates a user and returns JWT tokens
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auth response or MFA requirement signal</returns>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            result,
            result.RequiresMfa ? "MFA verification required" : "Login successful"
        ));
    }

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token
    /// </summary>
    /// <param name="request">The refresh token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New access and refresh tokens</returns>
    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            result,
            "Token refreshed"
        ));
    }

    /// <summary>
    /// Revokes a refresh token, preventing its future use
    /// </summary>
    /// <param name="request">The token to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("revoke-token")]
    public async Task<ActionResult<ApiResponse<bool>>> RevokeToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RevokeTokenAsync(
            request.RefreshToken,
            cancellationToken
        );

        if (!result)
            return NotFound(ApiResponse<bool>.ErrorResponse("Token not found"));

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Token revoked"));
    }

    #region Password Reset

    /// <summary>
    /// Initiates password reset flow by sending an email with a token
    /// </summary>
    /// <param name="request">Email address to reset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<string>>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request.Email, cancellationToken);

        return Ok(ApiResponse<string>.SuccessResponse(
            "If the email exists, a password reset link has been sent.",
            "Password reset email sent"
        ));
    }

    /// <summary>
    /// Resets password using the token received via email
    /// </summary>
    /// <param name="request">Token and new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<string>>> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);

        return Ok(ApiResponse<string>.SuccessResponse(
            "Password has been reset successfully.",
            "Password reset successful"
        ));
    }

    #endregion

    #region Multi-Factor Authentication

    /// <summary>
    /// Enables MFA for the current user and returns a QR code/secret
    /// </summary>
    /// <returns>QR code URL and manual entry key</returns>
    [HttpPost("enable-mfa")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<EnableMfaResponse>>> EnableMfa(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _authService.EnableMfaAsync(userId, cancellationToken);

        return Ok(ApiResponse<EnableMfaResponse>.SuccessResponse(
            result,
            "MFA setup initiated. Scan QR code and verify to complete."
        ));
    }

    /// <summary>
    /// Verifies the TOTP code and activates MFA if correct
    /// </summary>
    /// <param name="request">The 6-digit TOTP code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("verify-mfa")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> VerifyMfa(
        [FromBody] VerifyMfaRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _authService.VerifyAndEnableMfaAsync(userId, request.MfaCode, cancellationToken);

        if (!result)
            return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid MFA code"));

        return Ok(ApiResponse<bool>.SuccessResponse(true, "MFA enabled successfully"));
    }

    /// <summary>
    /// Disables MFA for the current user
    /// </summary>
    /// <returns>Success status</returns>
    [HttpPost("disable-mfa")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> DisableMfa(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.DisableMfaAsync(userId, cancellationToken);

        return Ok(ApiResponse<bool>.SuccessResponse(true, "MFA disabled successfully"));
    }

    #endregion
}
