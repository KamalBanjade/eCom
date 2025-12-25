using Commerce.Domain.Entities.Base;

namespace Commerce.Domain.Entities.Products;

/// <summary>
/// Represents a product category (e.g., "Electronics > Mobile Phones")
/// Supports hierarchical structure and SEO-friendly slugs.
/// </summary>
public class Category : BaseEntity
{
    /// <summary>
    /// Category name (e.g., "Laptops", "T-Shirts")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique URL-friendly slug (e.g., "laptops", "mens-t-shirts")
    /// Used for clean category URLs
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Optional detailed description of the category
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional image URL for the category banner or thumbnail
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Whether this category is visible in navigation and listings
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order in menus (lower = higher priority)
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Hierarchical structure (self-referencing)

    /// <summary>
    /// Parent category ID (null for root categories)
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Parent category navigation
    /// </summary>
    public Category? Parent { get; set; }

    /// <summary>
    /// Child/sub-categories
    /// </summary>
    public ICollection<Category> Children { get; set; } = new List<Category>();

    // Products in this category (direct assignment)

    /// <summary>
    /// Products directly assigned to this category
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();
}