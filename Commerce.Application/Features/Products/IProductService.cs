// File: Commerce.Application/Features/Products/IProductService.cs
using Commerce.Application.Features.Products.DTOs;

namespace Commerce.Application.Features.Products;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateProductAsync(string name, string description, decimal price, CancellationToken cancellationToken = default);
    Task<ProductVariantDto> CreateProductVariantAsync(
        Guid productId,
        string sku,
        decimal price,
        Dictionary<string, string> attributes,
        CancellationToken cancellationToken = default);
    Task<ProductVariantDto?> GetProductVariantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductVariantDto>> GetVariantsByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
}