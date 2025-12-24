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
    
    /// <summary>
    /// JSON dictionary of variant attributes (e.g., {"Size": "Large", "Color": "Red"})
    /// </summary>
    [Column(TypeName = "jsonb")]
    public Dictionary<string, string> Attributes { get; set; } = new();
    
    // Available stock for this variant
    public int AvailableStock { get; set; }
    
    // Foreign key to product
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
