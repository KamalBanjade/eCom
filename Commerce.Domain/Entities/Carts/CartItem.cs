using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Products;

namespace Commerce.Domain.Entities.Carts;

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    
    public int Quantity { get; set; }
    public decimal PriceAtAdd { get; set; } // Capture price when item added to cart
    
    public decimal SubTotal => Quantity * PriceAtAdd;
}
