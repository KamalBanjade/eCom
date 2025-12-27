// Commerce.Infrastructure/Services/UserProfileService.cs
using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Users.DTOs;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class UserProfileService : IUserProfileService
{
    private readonly CommerceDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserProfileService(CommerceDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<ApiResponse<UserProfileDto>> GetProfileAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user?.CustomerProfileId == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        var profile = await _context.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == user.CustomerProfileId.Value, cancellationToken);
            
        if (profile == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        return ApiResponse<UserProfileDto>.SuccessResponse(new UserProfileDto
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
        }, "Profile retrieved successfully");
    }

    public async Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(
        Guid userId, 
        UpdateProfileRequest request, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user?.CustomerProfileId == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        var profile = await _context.CustomerProfiles
            .FirstOrDefaultAsync(p => p.Id == user.CustomerProfileId.Value, cancellationToken);

        if (profile == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        profile.FirstName = request.FirstName;
        profile.LastName = request.LastName;
        profile.PhoneNumber = request.PhoneNumber;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<ApiResponse<UserProfileDto>> AddAddressAsync(
        Guid userId, 
        AddAddressRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Validate: Must be shipping, billing, or both
        if (!request.IsShipping && !request.IsBilling)
            return ApiResponse<UserProfileDto>.ErrorResponse("Address must be marked as shipping, billing, or both");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user?.CustomerProfileId == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        var profile = await _context.CustomerProfiles
            .FirstOrDefaultAsync(p => p.Id == user.CustomerProfileId.Value, cancellationToken);

        if (profile == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        // Add to shipping addresses
        if (request.IsShipping)
        {
            profile.ShippingAddresses ??= new();
            
            if (profile.ShippingAddresses.Count >= 5)
                return ApiResponse<UserProfileDto>.ErrorResponse("Maximum 5 shipping addresses allowed");
                
            profile.ShippingAddresses.Add(request.Address);
            
            // CRITICAL: Mark the JSON column as modified for EF Core to detect the change
            _context.Entry(profile).Property(p => p.ShippingAddresses).IsModified = true;
        }

        // Add to billing addresses
        if (request.IsBilling)
        {
            profile.BillingAddresses ??= new();
            
            if (profile.BillingAddresses.Count >= 5)
                return ApiResponse<UserProfileDto>.ErrorResponse("Maximum 5 billing addresses allowed");
                
            profile.BillingAddresses.Add(request.Address);
            
            // CRITICAL: Mark the JSON column as modified for EF Core to detect the change
            _context.Entry(profile).Property(p => p.BillingAddresses).IsModified = true;
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<ApiResponse<UserProfileDto>> UpdateAddressAsync(
        Guid userId, 
        UpdateAddressRequest request, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user?.CustomerProfileId == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        var profile = await _context.CustomerProfiles
            .FirstOrDefaultAsync(p => p.Id == user.CustomerProfileId.Value, cancellationToken);

        if (profile == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        // Update based on type
        if (request.Type == AddressType.Shipping)
        {
            if (profile.ShippingAddresses == null || 
                request.Index < 0 || 
                request.Index >= profile.ShippingAddresses.Count)
                return ApiResponse<UserProfileDto>.ErrorResponse("Invalid shipping address index");

            profile.ShippingAddresses[request.Index] = request.Address;
            
            // CRITICAL: Mark the JSON column as modified
            _context.Entry(profile).Property(p => p.ShippingAddresses).IsModified = true;
        }
        else if (request.Type == AddressType.Billing)
        {
            if (profile.BillingAddresses == null || 
                request.Index < 0 || 
                request.Index >= profile.BillingAddresses.Count)
                return ApiResponse<UserProfileDto>.ErrorResponse("Invalid billing address index");

            profile.BillingAddresses[request.Index] = request.Address;
            
            // CRITICAL: Mark the JSON column as modified
            _context.Entry(profile).Property(p => p.BillingAddresses).IsModified = true;
        }
        else
        {
            return ApiResponse<UserProfileDto>.ErrorResponse("Invalid address type");
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<ApiResponse<UserProfileDto>> RemoveAddressAsync(
        Guid userId, 
        RemoveAddressRequest request, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user?.CustomerProfileId == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        var profile = await _context.CustomerProfiles
            .FirstOrDefaultAsync(p => p.Id == user.CustomerProfileId.Value, cancellationToken);

        if (profile == null)
            return ApiResponse<UserProfileDto>.ErrorResponse("Profile not found");

        // Remove based on type
        if (request.Type == AddressType.Shipping)
        {
            if (profile.ShippingAddresses == null || 
                request.Index < 0 || 
                request.Index >= profile.ShippingAddresses.Count)
                return ApiResponse<UserProfileDto>.ErrorResponse("Invalid shipping address index");

            profile.ShippingAddresses.RemoveAt(request.Index);
            
            // CRITICAL: Mark the JSON column as modified
            _context.Entry(profile).Property(p => p.ShippingAddresses).IsModified = true;
        }
        else if (request.Type == AddressType.Billing)
        {
            if (profile.BillingAddresses == null || 
                request.Index < 0 || 
                request.Index >= profile.BillingAddresses.Count)
                return ApiResponse<UserProfileDto>.ErrorResponse("Invalid billing address index");

            profile.BillingAddresses.RemoveAt(request.Index);
            
            // CRITICAL: Mark the JSON column as modified
            _context.Entry(profile).Property(p => p.BillingAddresses).IsModified = true;
        }
        else
        {
            return ApiResponse<UserProfileDto>.ErrorResponse("Invalid address type");
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(userId, cancellationToken);
    }
}