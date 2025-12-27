using Commerce.Application.Common.Interfaces;
using Commerce.Domain.Entities.Carts;
using Commerce.Domain.Entities.Products;
using Commerce.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class PricingService : IPricingService
{
    private readonly IRepository<ProductVariant> _variantRepository;
    private readonly IRepository<Cart> _cartRepository;
    private readonly ICouponService _couponService;

    public PricingService(
        IRepository<ProductVariant> variantRepository,
        IRepository<Cart> cartRepository,
        ICouponService couponService)
    {
        _variantRepository = variantRepository;
        _cartRepository = cartRepository;
        _couponService = couponService;
    }

    public async Task<decimal> CalculatePriceAsync(Guid variantId, string? couponCode = null, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(variantId, cancellationToken);
        if (variant == null) throw new KeyNotFoundException($"Variant {variantId} not found");

        decimal price = variant.DiscountPrice ?? variant.Price;

        if (!string.IsNullOrEmpty(couponCode))
        {
            var coupon = await _couponService.ValidateCouponAsync(couponCode, cancellationToken);
            if (coupon != null)
            {
                price = ApplyDiscount(price, coupon);
            }
        }

        return price;
    }

    public async Task<decimal> CalculateCartTotalAsync(Guid cartId, string? couponCode = null, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetByIdAsync(cartId, cancellationToken, c => c.Items);
        if (cart == null) throw new KeyNotFoundException($"Cart {cartId} not found");

        decimal subtotal = 0;
        foreach (var item in cart.Items)
        {
            var variant = await _variantRepository.GetByIdAsync(item.ProductVariantId, cancellationToken);
            if (variant != null)
            {
                var itemPrice = variant.DiscountPrice ?? variant.Price;
                subtotal += itemPrice * item.Quantity;
            }
        }

        if (!string.IsNullOrEmpty(couponCode))
        {
            var coupon = await _couponService.ValidateCouponAsync(couponCode, cancellationToken);
            if (coupon != null)
            {
                // Simple logic: apply coupon to total (could be refined to specific items later)
                subtotal = ApplyDiscount(subtotal, coupon);
            }
        }

        return subtotal;
    }

    private decimal ApplyDiscount(decimal amount, Coupon coupon)
    {
        if (coupon.DiscountType == DiscountType.Percentage)
        {
            amount -= amount * (coupon.DiscountValue / 100);
        }
        else if (coupon.DiscountType == DiscountType.FixedAmount)
        {
            amount -= coupon.DiscountValue;
        }
        return Math.Max(0, amount);
    }
}
