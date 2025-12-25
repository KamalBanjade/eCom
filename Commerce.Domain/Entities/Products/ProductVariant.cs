using Commerce.Domain.Entities.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Commerce.Domain.Entities.Products;

/// <summary>
/// Product variant entity - represents a specific SKU with attributes (e.g., size, color)
/// </summary>
public class ProductVariant : BaseEntity
{
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    
    /// <summary>
    /// JSON dictionary of variant attributes (e.g., {"Size": "Large", "Color": "Red"})
    /// </summary>
    [Column(TypeName = "jsonb")]
    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(); // e.g., { "Color": "Red", "Size": "Large" }
    
    /// <summary>
    /// Optional variant-specific imag  e URL (overrides product images)
    /// </summary>
    public string? ImageUrl { get; set; }
    
    public int StockQuantity { get; set; }
    
    public bool IsActive { get; set; } = true;

    // Foreign key to product
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
