// File: Commerce.Application/Features/Products/IProductService.cs
using Commerce.Application.Features.Products.DTOs;

namespace Commerce.Application.Features.Products;

public interface IProductService
{
    // Product CRUD
    Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductResponse?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductResponse>> GetAllProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductResponse?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
    
    // Variant CRUD
    Task<ProductVariantResponse> CreateVariantAsync(Guid productId, CreateProductVariantRequest request, CancellationToken cancellationToken = default);
    Task<ProductVariantResponse?> GetVariantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductVariantResponse?> UpdateVariantAsync(Guid id, UpdateProductVariantRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteVariantAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductVariantResponse>> GetVariantsByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

    // Category
    Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);

    // Image Management
    Task<bool> AddProductImagesAsync(Guid productId, IEnumerable<string> imageUrls, CancellationToken cancellationToken = default);
    Task<bool> RemoveProductImageAsync(Guid productId, string imageUrl, CancellationToken cancellationToken = default);
    Task<bool> UpdateVariantImageAsync(Guid variantId, string imageUrl, CancellationToken cancellationToken = default);
    Task<bool> RemoveVariantImageAsync(Guid variantId, CancellationToken cancellationToken = default);
}