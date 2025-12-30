using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Users.DTOs;

namespace Commerce.Application.Features.Users;

/// <summary>
/// Service for managing internal users (Admin, Warehouse, Support)
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Creates a new internal user with specified role
    /// </summary>
    /// <param name="request">User creation details</param>
    /// <param name="creatorUserId">ID of the user creating this account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created user details</returns>
    Task<ApiResponse<AdminUserDto>> CreateInternalUserAsync(
        CreateAdminUserRequest request, 
        string creatorUserId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets paginated list of internal users with optional filtering
    /// </summary>
    /// <param name="filter">Filter parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of users</returns>
    Task<PagedResult<AdminUserDto>> GetInternalUsersAsync(
        AdminUserFilterRequest filter, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User details or null if not found</returns>
    Task<AdminUserDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a user's role
    /// </summary>
    /// <param name="userId">User ID to update</param>
    /// <param name="newRole">New role to assign</param>
    /// <param name="updaterUserId">ID of user performing the update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated user details</returns>
    Task<ApiResponse<AdminUserDto>> UpdateUserRoleAsync(
        string userId, 
        string newRole, 
        string updaterUserId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Triggers password reset email for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    Task<ApiResponse<bool>> TriggerPasswordResetAsync(string userId, CancellationToken cancellationToken = default);
}
