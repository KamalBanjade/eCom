using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Users.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Commerce.API.Controllers;

[Route("api/admin/profiles")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Warehouse,Support")] // All internal users have profiles
public class AdminProfilesController : ControllerBase
{
    private readonly IUserProfileService _profileService;

    public AdminProfilesController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>
    /// Internal User: Get own profile
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> GetMyProfile(CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var result = await _profileService.GetInternalUserProfileAsync(currentUserId, cancellationToken);
        if (result == null) return NotFound(ApiResponse<AdminUserDto>.ErrorResponse("Profile not found"));
        
        return Ok(ApiResponse<AdminUserDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Internal User: Update own profile (Phone only currently)
    /// </summary>
    [HttpPatch("me")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> UpdateMyProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var result = await _profileService.UpdateInternalUserProfileAsync(currentUserId, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        
        return Ok(result);
    }
    
    /// <summary>
    /// Admin: Get any internal user's profile
    /// </summary>
    [HttpGet("{userId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> GetUserProfile(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.GetInternalUserProfileAsync(userId, cancellationToken);
        if (result == null) return NotFound(ApiResponse<AdminUserDto>.ErrorResponse("User not found"));
        
        return Ok(ApiResponse<AdminUserDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Update any internal user's profile
    /// </summary>
    [HttpPatch("{userId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> UpdateUserProfile(
        string userId,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.UpdateInternalUserProfileAsync(userId, request, cancellationToken);
        if (!result.Success) return BadRequest(result);
        
        return Ok(result);
    }
}
