using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Users;
using Commerce.Application.Features.Users.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Commerce.API.Controllers;

[Route("api/admin/users")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")] // Base policy, specific methods have tighter checks
public class AdminUsersController : ControllerBase
{
    private readonly IUserManagementService _userService;

    public AdminUsersController(IUserManagementService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Admin: Create a new internal user (Admin, Warehouse, Support)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> CreateUser(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var result = await _userService.CreateInternalUserAsync(request, currentUserId, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return StatusCode(201, result);
    }

    /// <summary>
    /// Admin: List all internal users with filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminUserDto>>>> GetUsers(
        [FromQuery] AdminUserFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetInternalUsersAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminUserDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Get specific user details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> GetUserById(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetUserByIdAsync(id, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<AdminUserDto>.ErrorResponse("User not found"));

        return Ok(ApiResponse<AdminUserDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Change a user's role
    /// </summary>
    [HttpPatch("{id}/role")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> UpdateUserRole(
        string id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var result = await _userService.UpdateUserRoleAsync(id, request.NewRole, currentUserId, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Admin: Trigger password reset email
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<ActionResult<ApiResponse<bool>>> TriggerPasswordReset(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _userService.TriggerPasswordResetAsync(id, cancellationToken);
        if (!result.Success) return BadRequest(result);
        
        return Ok(result);
    }
}
