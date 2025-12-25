
namespace Commerce.Application.Features.Products.DTOs;

public record CategoryDto(Guid Id, string Name, string Description, bool IsActive);

public record CreateProductRequest(
    string Name, 
    string Description, 
    decimal BasePrice, 
    Guid CategoryId, 
    string? Brand);

public record UpdateProductRequest(
    string Name, 
    string Description, 
    decimal BasePrice, 
    Guid CategoryId, 
    string? Brand, 
    bool IsActive);

public record ProductResponse(
    Guid Id, 
    string Name, 
    string Description, 
    decimal BasePrice, 
    string CategoryName, 
    Guid CategoryId,
    string? Brand, 
    bool IsActive, 
    DateTime CreatedAt,
    List<ProductVariantResponse> Variants);

public record CreateProductVariantRequest(
    string SKU, 
    decimal Price, 
    decimal? DiscountPrice, 
    int StockQuantity, 
    Dictionary<string, string> Attributes);

public record UpdateProductVariantRequest(
    string SKU, 
    decimal Price, 
    decimal? DiscountPrice, 
    int StockQuantity, 
    Dictionary<string, string> Attributes, 
    bool IsActive);

public record ProductVariantResponse(
    Guid Id, 
    Guid ProductId, 
    string SKU, 
    decimal Price, 
    decimal? DiscountPrice, 
    int StockQuantity, 
    Dictionary<string, string> Attributes, 
    bool IsActive);
