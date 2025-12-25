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
    
    // Foreign key
    public Guid CategoryId { get; set; }
    
    // Navigation property
    public Category Category { get; set; } = null!;
    
    public string? Brand { get; set; }
    
    /// <summary>
    /// List of image URLs for this product (first is primary)
    /// </summary>
    public List<string> ImageUrls { get; set; } = new List<string>();
    
    public bool IsActive { get; set; } = true;
    
    // One-to-many with variants
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}