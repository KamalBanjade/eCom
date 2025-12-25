using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Products;

namespace Commerce.Domain.Entities.Inventory;

/// <summary>
/// Represents a temporary reservation of stock (e.g., items in cart or during checkout).
/// Prevents overselling by holding stock for a limited time.
/// </summary>
public class StockReservation : BaseEntity
{
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;

    public int Quantity { get; set; }
    
    // Can be UserId or a session/cart identifier
    public string? UserId { get; set; }

    public DateTime ExpiresAt { get; set; }
    
    // Status flags
    public bool IsReleased { get; set; } // Expired or manually released (removed from cart)
    public bool IsConfirmed { get; set; } // Converted to an order
}
