namespace Commerce.Application.Features.Products.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public bool IsActive { get; set; }
    public List<ProductVariantDto> Variants { get; set; } = new();
}

public class ProductVariantDto
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public int AvailableStock { get; set; }
}
