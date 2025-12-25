using Commerce.Application.Common.DTOs;

namespace Commerce.Application.Features.Carts;

public interface ICartService
{
    Task<ApiResponse<CartResponse>> GetCartAsync(
        Guid? applicationUserId,
        string? anonymousId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> AddItemAsync(
        Guid? applicationUserId,
        string? anonymousId,
        Guid productVariantId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> RemoveItemAsync(
        Guid? applicationUserId,
        string? anonymousId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> ClearCartAsync(
        Guid? applicationUserId,
        string? anonymousId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> TransferAnonymousCartToCustomerAsync(
        string anonymousId,
        Guid applicationUserId,
        CancellationToken cancellationToken = default);
}