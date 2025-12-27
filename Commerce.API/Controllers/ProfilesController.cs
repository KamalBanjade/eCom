// Commerce.API/Controllers/ProfilesController.cs
using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Users.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Customer")]
public class ProfilesController : ControllerBase
{
    private readonly IUserProfileService _profileService;

    public ProfilesController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User ID not found");
        return Guid.Parse(userId);
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile(
        CancellationToken cancellationToken)
    {
        var result = await _profileService.GetProfileAsync(GetUserId(), cancellationToken);
        
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Update profile information (name, phone)
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.UpdateProfileAsync(GetUserId(), request, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Add new address (can be shipping, billing, or both)
    /// </summary>
    [HttpPost("addresses")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> AddAddress(
        [FromBody] AddAddressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.AddAddressAsync(GetUserId(), request, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Update existing address by type and index
    /// </summary>
    [HttpPut("addresses")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateAddress(
        [FromBody] UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.UpdateAddressAsync(GetUserId(), request, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Remove address by type and index
    /// </summary>
    [HttpDelete("addresses")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> RemoveAddress(
        [FromQuery] AddressType type,
        [FromQuery] int index,
        CancellationToken cancellationToken)
    {
        var request = new RemoveAddressRequest { Type = type, Index = index };
        var result = await _profileService.RemoveAddressAsync(GetUserId(), request, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}