using Commerce.Domain.Entities.Base;

namespace Commerce.Domain.Entities.Products;

/// <summary>
/// Product aggregate root - represents a product with its variants
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation property to variants
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
