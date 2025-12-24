using Commerce.Domain.Entities.Base;

namespace Commerce.Domain.Entities.Products;

/// <summary>
/// Represents a product variant (e.g., different sizes, colors)
/// </summary>
public class ProductVariant : BaseEntity
{
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    
    // JSON field for flexible attributes like {"Color": "Red", "Size": "M"}
    public Dictionary<string, string> Attributes { get; set; } = new();
    
    // Foreign key to Product
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
