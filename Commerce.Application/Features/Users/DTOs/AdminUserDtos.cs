namespace Commerce.Application.Features.Users.DTOs;

/// <summary>
/// DTO for internal user (Admin, Warehouse, Support)
/// </summary>
public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool MfaEnabled { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to create a new internal user
/// </summary>
public class CreateAdminUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // Admin, Warehouse, or Support
    public string? PhoneNumber { get; set; }
    public string? TemporaryPassword { get; set; } // Optional, will be auto-generated if not provided
}

/// <summary>
/// Request to update a user's role
/// </summary>
public class UpdateUserRoleRequest
{
    public string NewRole { get; set; } = string.Empty;
}

/// <summary>
/// Filter request for admin users
/// </summary>
public class AdminUserFilterRequest
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? SearchTerm { get; set; } // Search by email
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
