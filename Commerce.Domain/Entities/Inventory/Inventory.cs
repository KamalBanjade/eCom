using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Products;

namespace Commerce.Domain.Entities.Inventory;

public class Inventory : BaseEntity
{
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int ReorderLevel { get; set; } = 10;
    
    public int TotalQuantity => AvailableQuantity + ReservedQuantity;
    public bool IsLowStock => AvailableQuantity <= ReorderLevel;
}
