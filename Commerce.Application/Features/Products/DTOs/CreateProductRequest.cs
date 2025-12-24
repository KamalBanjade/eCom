namespace Commerce.Application.Features.Products.DTOs;

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }
}
