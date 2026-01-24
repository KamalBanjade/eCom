using Commerce.Domain.Entities.Sales;

namespace Commerce.Application.Common.Interfaces;

public interface ICouponService
{
    Task<Coupon?> ValidateCouponAsync(string code, CancellationToken cancellationToken = default);
    
    Task<Coupon> ValidateAndRegisterUsageAsync(string code, decimal orderSubtotal, CancellationToken cancellationToken = default);

    Task<Coupon> CreateCouponAsync(Coupon coupon, CancellationToken cancellationToken = default);
    
    Task<Coupon?> GetCouponByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<Coupon>> GetAllCouponsAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteCouponAsync(string code, CancellationToken cancellationToken = default);
    Task<Coupon> UpdateCouponAsync(Coupon coupon, CancellationToken cancellationToken = default);
}