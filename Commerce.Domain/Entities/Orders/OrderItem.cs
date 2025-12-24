using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Products;

namespace Commerce.Domain.Entities.Orders;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    
    // Snapshot of product details at order time (stored as JSON)
    public Dictionary<string, string> ProductSnapshot { get; set; } = new();
    
    public decimal SubTotal => Quantity * UnitPrice;
}
