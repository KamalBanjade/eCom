using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Users;
using Commerce.Application.Features.Users.DTOs;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Commerce.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<ApiResponse<AdminUserDto>> CreateInternalUserAsync(
        CreateAdminUserRequest request, 
        string creatorUserId, 
        CancellationToken cancellationToken = default)
    {
        // 1. Check Creator Roles to enforce hierarchy
        var creator = await _userManager.FindByIdAsync(creatorUserId);
        if (creator == null)
            return ApiResponse<AdminUserDto>.ErrorResponse("Creator user not found");
        
        var creatorRoles = await _userManager.GetRolesAsync(creator);
        var isSuperAdmin = creatorRoles.Contains(UserRoles.SuperAdmin);
        var isAdmin = creatorRoles.Contains(UserRoles.Admin);

        if (!isSuperAdmin && !isAdmin)
            return ApiResponse<AdminUserDto>.ErrorResponse("Unauthorized to create users");

        // Role Validation Hierarchy
        // SuperAdmin can create Admin, Warehouse, Support
        // Admin can create Warehouse, Support (NOT Admin or SuperAdmin)
        
        if (request.Role == UserRoles.SuperAdmin)
             return ApiResponse<AdminUserDto>.ErrorResponse("Cannot create SuperAdmin via API");

        if (request.Role == UserRoles.Admin && !isSuperAdmin)
             return ApiResponse<AdminUserDto>.ErrorResponse("Only SuperAdmin can create Admin users");

        if (!await _roleManager.RoleExistsAsync(request.Role))
             return ApiResponse<AdminUserDto>.ErrorResponse($"Role {request.Role} does not exist");

        // 2. Create User WITHOUT password
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
             return ApiResponse<AdminUserDto>.ErrorResponse("Email is already registered");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true, // Internal users pre-verified
            // CreatedAt = DateTime.UtcNow
        };

        // Create user without password - they will set it via email link
        var result = await _userManager.CreateAsync(user);

        if (!result.Succeeded)
            return ApiResponse<AdminUserDto>.ErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));

        // 3. Assign Role
        await _userManager.AddToRoleAsync(user, request.Role);

        // 4. Generate password reset token
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        
        // 5. Build password setup link (using AdminAppUrl from configuration)
        var adminAppUrl = _configuration["AdminAppUrl"] ?? "https://localhost:7213";
        var resetLink = $"{adminAppUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(user.Email)}";

        // 6. Send single welcome email with password setup link
        await _emailService.SendWelcomeWithPasswordSetupAsync(
            email: user.Email!,
            role: request.Role,
            resetLink: resetLink,
            cancellationToken: cancellationToken
        );

        return ApiResponse<AdminUserDto>.SuccessResponse(await MapToDto(user));
    }

    public async Task<PagedResult<AdminUserDto>> GetInternalUsersAsync(
        AdminUserFilterRequest filter, 
        CancellationToken cancellationToken = default)
    {
        // Get all users who have internal roles
        var internalRoles = new[] { UserRoles.Admin, UserRoles.Warehouse, UserRoles.Support, UserRoles.SuperAdmin };
        
        // This is inefficient in pure Identity without a custom join, but standard approach:
        // Filter by role requires joining UserRoles
        // For efficiency, we can query users directly if we assume they are internal based on some flag, but here we rely on roles.
        
        var query = _userManager.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(filter.SearchTerm))
            query = query.Where(u => u.Email!.Contains(filter.SearchTerm) || u.UserName!.Contains(filter.SearchTerm));

        // Note: Filtering by role in EF Core with Identity is tricky without direct DB context access or manual join.
        // We'll fetch users and filter in memory if role filter is applied, OR use a join strategy if available.
        // Given complexity, let's fetch matching users then filter by role if needed. 
        // Better: join explicitly if we had DbContext. 
        // For now, simpler approach:
        
        var users = await query.ToListAsync(cancellationToken);
        var adminUsers = new List<AdminUserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Any(r => internalRoles.Contains(r)))
            {
                // Apply Role filter if specified
                if (!string.IsNullOrEmpty(filter.Role) && !roles.Contains(filter.Role))
                    continue;
                    
                adminUsers.Add(await MapToDto(user, roles));
            }
        }
        
        // Paging in memory (Not ideal for millions, but fine for admin users)
        var totalCount = adminUsers.Count;
        var pagedUsers = adminUsers
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedResult<AdminUserDto>(pagedUsers, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<AdminUserDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;
        
        return await MapToDto(user);
    }

    public async Task<ApiResponse<AdminUserDto>> UpdateUserRoleAsync(
        string userId, 
        string newRole, 
        string updaterUserId, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return ApiResponse<AdminUserDto>.ErrorResponse("User not found");

        // Prevents modifying SuperAdmin checks
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(UserRoles.SuperAdmin))
            return ApiResponse<AdminUserDto>.ErrorResponse("Cannot modify SuperAdmin user");

        // Check Updater
        var updater = await _userManager.FindByIdAsync(updaterUserId);
        var updaterRoles = await _userManager.GetRolesAsync(updater!);
        
        bool isSuperAdmin = updaterRoles.Contains(UserRoles.SuperAdmin);
        
        // Only SuperAdmin can promote/demote generic Admins
        if (roles.Contains(UserRoles.Admin) && !isSuperAdmin)
             return ApiResponse<AdminUserDto>.ErrorResponse("Only SuperAdmin can modify Admin users");

        if (newRole == UserRoles.Admin && !isSuperAdmin)
             return ApiResponse<AdminUserDto>.ErrorResponse("Only SuperAdmin can assign Admin role");
             
        if (!await _roleManager.RoleExistsAsync(newRole))
             return ApiResponse<AdminUserDto>.ErrorResponse("Role does not exist");
             
        // Remove old roles
        await _userManager.RemoveFromRolesAsync(user, roles);
        // Add new role
        await _userManager.AddToRoleAsync(user, newRole);
        
        return ApiResponse<AdminUserDto>.SuccessResponse(await MapToDto(user));
    }

    public async Task<ApiResponse<bool>> TriggerPasswordResetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return ApiResponse<bool>.ErrorResponse("User not found");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // In a real app, generate a link. For API demo, we might just email the token or a link FE handles.
        // Assuming FE url:
        var resetLink = $"https://admin.ecommerce.com/reset-password?token={Uri.EscapeDataString(token)}&email={user.Email}";
        
        await _emailService.SendEmailAsync(
            user.Email!, 
            "Password Reset Request", 
            $"<p>Click <a href='{resetLink}'>here</a> to reset your password.</p>"
        );

        return ApiResponse<bool>.SuccessResponse(true, "Password reset email sent");
    }

    private async Task<AdminUserDto> MapToDto(ApplicationUser user, IList<string>? roles = null)
    {
        if (roles == null)
            roles = await _userManager.GetRolesAsync(user);

        return new AdminUserDto
        {
            Id = user.Id,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            MfaEnabled = user.MfaEnabled,
            Roles = roles.ToList(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true // assuming identity users are active unless locked out
        };
    }
}
