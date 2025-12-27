// Commerce.Application/Common/Interfaces/IUserProfileService.cs
using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Users.DTOs;

namespace Commerce.Application.Common.Interfaces;

public interface IUserProfileService
{
    // Get profile
    Task<ApiResponse<UserProfileDto>> GetProfileAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);
    
    // Update profile info (name, phone)
    Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(
        Guid userId, 
        UpdateProfileRequest request, 
        CancellationToken cancellationToken = default);

    // Add address (supports both shipping and billing in one call)
    Task<ApiResponse<UserProfileDto>> AddAddressAsync(
        Guid userId, 
        AddAddressRequest request, 
        CancellationToken cancellationToken = default);

    // Update specific address by index and type
    Task<ApiResponse<UserProfileDto>> UpdateAddressAsync(
        Guid userId, 
        UpdateAddressRequest request, 
        CancellationToken cancellationToken = default);

    // Remove specific address by index and type
    Task<ApiResponse<UserProfileDto>> RemoveAddressAsync(
        Guid userId, 
        RemoveAddressRequest request, 
        CancellationToken cancellationToken = default);
}