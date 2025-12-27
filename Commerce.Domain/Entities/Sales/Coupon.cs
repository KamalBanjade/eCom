using Commerce.Domain.Entities.Base;

namespace Commerce.Domain.Entities.Sales;

public enum DiscountType
{
    Percentage,
    FixedAmount
}

public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Optional: Usage limits
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; }
    
    // Optional: Min order amount
    public decimal? MinOrderAmount { get; set; }

    public bool IsValid()
    {
        return IsActive && ExpiryDate > DateTime.UtcNow && (MaxUses == null || CurrentUses < MaxUses);
    }
}
