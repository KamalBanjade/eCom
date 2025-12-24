using Commerce.Application.Common.DTOs;
using Commerce.Domain.Entities.Carts;

namespace Commerce.Application.Features.Carts;

public interface ICartService
{
    Task<ApiResponse<Cart>> GetCartAsync(Guid? customerId, string? anonymousId, CancellationToken cancellationToken = default);
    Task<ApiResponse<Cart>> AddItemAsync(Guid? customerId, string? anonymousId, Guid productVariantId, int quantity, CancellationToken cancellationToken = default);
    Task<ApiResponse<Cart>> RemoveItemAsync(Guid? customerId, string? anonymousId, Guid itemId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> TransferAnonymousCartToCustomerAsync(string anonymousId, Guid customerId, CancellationToken cancellationToken = default);
}
