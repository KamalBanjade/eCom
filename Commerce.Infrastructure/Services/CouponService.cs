using Commerce.Application.Common.Interfaces;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Entities.Carts;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class CouponService : ICouponService
{
    private readonly IRepository<Coupon> _couponRepository;


    public CouponService(IRepository<Coupon> couponRepository)
    {
        _couponRepository = couponRepository;
    }

    public async Task<Coupon?> ValidateCouponAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        string normalizedCode = code.Trim().ToUpperInvariant();

        var coupons = await _couponRepository.GetAsync(
            c => c.Code == normalizedCode && c.IsActive,
            cancellationToken);

        var coupon = coupons.FirstOrDefault();
        if (coupon == null || coupon.ExpiryDate < DateTime.UtcNow)
            return null;

        if (coupon.MaxUses.HasValue && coupon.CurrentUses >= coupon.MaxUses.Value)
            return null;

        return coupon;
    }

    public async Task<Coupon> ValidateAndRegisterUsageAsync(string code, decimal orderSubtotal, CancellationToken cancellationToken = default)
    {
        var coupon = await ValidateCouponAsync(code, cancellationToken);
        if (coupon == null)
            throw new InvalidOperationException("Coupon is invalid, expired, or usage limit reached.");

        if (orderSubtotal <= 0)
            throw new InvalidOperationException("Order subtotal is zero.");

        if (coupon.MinOrderAmount.HasValue && orderSubtotal < coupon.MinOrderAmount.Value)
            throw new InvalidOperationException($"Minimum order amount of {coupon.MinOrderAmount} not met.");

        // Increment usage
        // Note: Ideally usage is incremented on Order Placement, not on Cart Apply.
        // But following existing logic, we increment here.
        // This might cause issues if user never checks out (usage leaked).
        // However, for this task scope, we preserve the behavior requested.
        coupon.CurrentUses += 1;
        await _couponRepository.UpdateAsync(coupon, cancellationToken);
        await _couponRepository.SaveChangesAsync(cancellationToken);

        return coupon;
    }

    public async Task<Coupon> CreateCouponAsync(Coupon coupon, CancellationToken cancellationToken = default)
    {
        string normalizedCode = coupon.Code.Trim().ToUpperInvariant();

        var existing = await _couponRepository.GetAsync(c => c.Code == normalizedCode, cancellationToken);
        if (existing.Any())
            throw new InvalidOperationException($"Coupon with code '{normalizedCode}' already exists");

        coupon.Code = normalizedCode;
        coupon.CurrentUses = 0;
        coupon.IsActive = true;

        await _couponRepository.AddAsync(coupon, cancellationToken);
        await _couponRepository.SaveChangesAsync(cancellationToken);

        return coupon;
    }

    public async Task<Coupon?> GetCouponByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        string normalizedCode = code.Trim().ToUpperInvariant();

        var coupons = await _couponRepository.GetAsync(c => c.Code == normalizedCode, cancellationToken);
        return coupons.FirstOrDefault();
    }

    public async Task<bool> DeactivateCouponAsync(string code, CancellationToken cancellationToken = default)
    {
        var coupon = await GetCouponByCodeAsync(code, cancellationToken);
        if (coupon == null)
            return false;

        coupon.IsActive = false;
        await _couponRepository.UpdateAsync(coupon, cancellationToken);
        await _couponRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}