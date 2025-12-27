using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Users;

namespace Commerce.Domain.Entities.Carts;

/// <summary>
/// Cart aggregate root - enforces cart identity constraint
/// </summary>
public class Cart : BaseEntity
{
    // INVARIANT: Exactly one of CustomerProfileId or AnonymousId must be set
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }
    
    public string? AnonymousId { get; private set; }
    
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    public string? AppliedCouponCode { get; set; }
    public DateTime ExpiresAt { get; set; }
    
    // Public parameterless constructor for Serialization
    public Cart() 
    { 
        ExpiresAt = DateTime.UtcNow.AddDays(30); 
    }
    
    // Factory methods to enforce invariants
    public static Cart CreateForCustomer(Guid customerProfileId)
    {
        return new Cart
        {
            CustomerProfileId = customerProfileId,
            AnonymousId = null
        };
    }
    
    public static Cart CreateAnonymous(string anonymousId)
    {
        if (string.IsNullOrWhiteSpace(anonymousId))
            throw new ArgumentException("Anonymous ID cannot be empty", nameof(anonymousId));
            
        return new Cart
        {
            CustomerProfileId = null,
            AnonymousId = anonymousId
        };
    }
    
    // Explicit ownership transfer method
    public void TransferToCustomer(Guid customerProfileId)
    {
        if (CustomerProfileId.HasValue)
            throw new InvalidOperationException("Cart is already owned by a customer");
            
        CustomerProfileId = customerProfileId;
        AnonymousId = null;
    }
    
    // Validation method
    public bool IsValid()
    {
        // Exactly one of CustomerProfileId or AnonymousId must be set
        return (CustomerProfileId.HasValue && string.IsNullOrEmpty(AnonymousId)) ||
               (!CustomerProfileId.HasValue && !string.IsNullOrEmpty(AnonymousId));
    }
}
