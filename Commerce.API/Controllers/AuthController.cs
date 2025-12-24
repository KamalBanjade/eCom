using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Auth.DTOs;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Login), new { email = request.Email }, ApiResponse<AuthResponse>.SuccessResponse(result, "Registration successful"));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Login successful"));
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Token refreshed successfully"));
    }

    [HttpPost("revoke-token")]
    public async Task<ActionResult<ApiResponse<bool>>> RevokeToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RevokeTokenAsync(request.RefreshToken, cancellationToken);
        if (!result)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Token not found or already revoked"));
        }
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Token revoked successfully"));
    }
}
