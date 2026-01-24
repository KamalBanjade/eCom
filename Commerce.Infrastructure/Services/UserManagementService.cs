using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Returns.DTOs;
using Commerce.Application.Features.Users;
using Commerce.Application.Features.Users.DTOs;
using Commerce.Infrastructure.Data;
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
    private readonly IOrderService _orderService;
    private readonly IReturnService _returnService;
    private readonly CommerceDbContext _context;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        IConfiguration configuration,
        IOrderService orderService,
        IReturnService returnService,
        CommerceDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _configuration = configuration;
        _orderService = orderService;
        _returnService = returnService;
        _context = context;
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
        var adminAppUrl = _configuration["AdminAppUrl"] ?? "http://localhost:3000";
        var resetLink = $"{adminAppUrl}/auth/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(user.Email)}";

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
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        // Get all users who have internal roles
        var internalRoles = new[] { UserRoles.Admin, UserRoles.Warehouse, UserRoles.Support, UserRoles.SuperAdmin };
        
        var query = _userManager.Users.AsNoTracking().AsQueryable();

        // Filter out current user
        query = query.Where(u => u.Id != currentUserId);

        if (!string.IsNullOrEmpty(filter.SearchTerm))
            query = query.Where(u => u.Email!.Contains(filter.SearchTerm) || u.UserName!.Contains(filter.SearchTerm));

        var users = await query.ToListAsync(cancellationToken);
        var adminUsers = new List<AdminUserDto>();

        // Check if requester is SuperAdmin
        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        var currentUserRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : new List<string>();
        var isSuperAdmin = currentUserRoles.Contains(UserRoles.SuperAdmin);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Any(r => internalRoles.Contains(r)))
            {
                // Filter: If not SuperAdmin, cannot see SuperAdmins
                if (!isSuperAdmin && roles.Contains(UserRoles.SuperAdmin))
                    continue;

                // Apply Role filter if specified
                if (!string.IsNullOrEmpty(filter.Role) && !roles.Contains(filter.Role))
                    continue;
                    
                adminUsers.Add(await MapToDto(user, roles));
            }
        }
        
        // Paging in memory
        var totalCount = adminUsers.Count;
        var pagedUsers = adminUsers
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedResult<AdminUserDto>(pagedUsers, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<PagedResult<AdminUserDto>> GetExternalUsersAsync(
        AdminUserFilterRequest filter, 
        CancellationToken cancellationToken = default)
    {
        // Get all users who have the Customer role
        var query = _userManager.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(filter.SearchTerm))
            query = query.Where(u => u.Email!.Contains(filter.SearchTerm) || u.UserName!.Contains(filter.SearchTerm));

        var users = await query.ToListAsync(cancellationToken);
        var externalUsers = new List<AdminUserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(UserRoles.Customer))
            {
                // Apply status filter if specified
                if (filter.IsActive.HasValue && user.IsActive != filter.IsActive.Value)
                    continue;
                    
                externalUsers.Add(await MapToDto(user, roles));
            }
        }
        
        // Paging in memory
        var totalCount = externalUsers.Count;
        var pagedUsers = externalUsers
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

    public async Task<ApiResponse<AdminUserDto>> UpdateUserStatusAsync(
        string userId, 
        bool isActive, 
        string updaterUserId, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return ApiResponse<AdminUserDto>.ErrorResponse("User not found");

        // Prevents deactivating SuperAdmin
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(UserRoles.SuperAdmin) && !isActive)
            return ApiResponse<AdminUserDto>.ErrorResponse("Cannot deactivate a SuperAdmin user");

        // Check Updater
        var updater = await _userManager.FindByIdAsync(updaterUserId);
        var updaterRoles = await _userManager.GetRolesAsync(updater!);
        bool isSuperAdmin = updaterRoles.Contains(UserRoles.SuperAdmin);

        // Only Admin/SuperAdmin can change status
        // Admin cannot change Admin/SuperAdmin status
        if (roles.Contains(UserRoles.SuperAdmin) || (roles.Contains(UserRoles.Admin) && !isSuperAdmin))
            return ApiResponse<AdminUserDto>.ErrorResponse("Insufficient permissions to change this user's status");

        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return ApiResponse<AdminUserDto>.ErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));

        return ApiResponse<AdminUserDto>.SuccessResponse(await MapToDto(user, roles));
    }

    public async Task<ApiResponse<bool>> TriggerPasswordResetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return ApiResponse<bool>.ErrorResponse("User not found");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var adminAppUrl = _configuration["AdminAppUrl"] ?? "http://localhost:3000";
        var resetLink = $"{adminAppUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";
        
        await _emailService.SendForgotPasswordEmailAsync(user.Email!, resetLink, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Password reset email sent");
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(string userId, string requesterId, CancellationToken cancellationToken = default)
    {
        if (userId == requesterId)
            return ApiResponse<bool>.ErrorResponse("You cannot delete your own account");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return ApiResponse<bool>.ErrorResponse("User not found");

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(UserRoles.SuperAdmin))
        {
            // Only allow specialized handling if needed, but generally SuperAdmin deletion is dangerous
            // For now, disallow entirely via this API
            return ApiResponse<bool>.ErrorResponse("Cannot delete a SuperAdmin user");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return ApiResponse<bool>.ErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ApiResponse<bool>.SuccessResponse(true, "User deleted successfully");
    }

    public async Task<ApiResponse<CustomerDetailDto>> GetExternalUserDetailAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) 
            return ApiResponse<CustomerDetailDto>.ErrorResponse("User not found");

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(UserRoles.Customer))
            return ApiResponse<CustomerDetailDto>.ErrorResponse("User is not an external customer");

        // 1. Get User DTO
        var userDto = await MapToDto(user, roles);

        // 2. Get Profile DTO
        var profile = await _context.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id, cancellationToken);
        
        if (profile == null)
            return ApiResponse<CustomerDetailDto>.ErrorResponse("Customer profile not found");

        var profileDto = new UserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Email = profile.Email,
            PhoneNumber = profile.PhoneNumber,
            ShippingAddresses = profile.ShippingAddresses ?? new(),
            BillingAddresses = profile.BillingAddresses ?? new(),
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };

        // 3. Get Orders
        // OrderService already has GetUserOrdersAsync that takes Guid applicationUserId
        var orders = await _orderService.GetUserOrdersAsync(Guid.Parse(user.Id), cancellationToken);

        // 4. Get Returns
        var returns = await _returnService.GetUserReturnsAsync(Guid.Parse(user.Id), cancellationToken);
        
        // Map Returns to DTO (ReturnService.GetUserReturnsAsync returns IEnumerable<ReturnRequest>)
        // We need ReturnRequestDto. 
        // We can reuse MapToDto logic or just call GetAllReturnsAsync with filter, but that's less efficient.
        // Actually, let's see if we can get ReturnRequestDto from ReturnService.
        
        // Since MapToDto is private in ReturnService, we might need to expose a method or re-map.
        // For now, I'll do a simple mapping or check if I can add a method to ReturnService.
        
        var returnDtos = returns.Select(r => new ReturnRequestDto
        {
            Id = r.Id,
            OrderId = r.OrderId,
            OrderNumber = r.Order?.OrderNumber ?? "N/A",
            ReturnStatus = r.ReturnStatus.ToString(),
            RefundAmount = r.TotalRefundAmount,
            RefundMethod = r.RefundMethod?.ToString(),
            RefundedAt = r.RefundedAt,
            RequestedAt = r.RequestedAt,
            Reason = string.Join(", ", r.Items?.Select(i => i.Reason) ?? Enumerable.Empty<string>())
        }).ToList();

        return ApiResponse<CustomerDetailDto>.SuccessResponse(new CustomerDetailDto
        {
            User = userDto,
            Profile = profileDto,
            Orders = orders.ToList(),
            Returns = returnDtos
        });
    }

    private async Task<AdminUserDto> MapToDto(ApplicationUser user, IList<string>? roles = null)
    {
        if (roles == null)
            roles = await _userManager.GetRolesAsync(user);

        // Determine status
        // Pending: Active but no password set (new user invited via email)
        // Active: Active and has set a password
        // Inactive: Explicitly deactivated
        string status;
        if (!user.IsActive)
            status = "Inactive";
        else if (user.PasswordHash == null)
            status = "Pending";
        else
            status = "Active";

        return new AdminUserDto
        {
            Id = user.Id,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            MfaEnabled = user.MfaEnabled,
            Roles = roles.ToList(),
            CreatedAt = DateTime.UtcNow,
            IsActive = user.IsActive, 
            Status = status
        };
    }
}
