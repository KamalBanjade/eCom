using Commerce.Application.Features.Products.DTOs;
using Commerce.Domain.Entities.Sales;

namespace Commerce.Application.Common.Interfaces;

public interface IPricingService
{
    Task<decimal> CalculatePriceAsync(Guid variantId, string? couponCode = null, CancellationToken cancellationToken = default);
    Task<decimal> CalculateCartTotalAsync(Guid cartId, string? couponCode = null, CancellationToken cancellationToken = default);
}
