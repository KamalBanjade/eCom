using Commerce.Domain.Entities.Sales;
using System.Text.Json.Serialization;

namespace Commerce.API.DTOs;

public record CreateCouponRequest(string Code, DiscountType DiscountType, decimal DiscountValue, DateTime ExpiryDate, int? MaxUses, decimal? MinOrderAmount);
public record UpdateCouponRequest(bool IsActive); // Simplified for phase 3
public record ApplyCouponRequest(
    [property: JsonPropertyName("code")] string Code, 
    [property: JsonPropertyName("cartId")] string CartId);
