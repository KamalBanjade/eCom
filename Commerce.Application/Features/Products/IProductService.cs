// File: Commerce.Application/Features/Products/IProductService.cs
using Commerce.Application.Features.Products.DTOs;
using Commerce.Application.Common.DTOs;

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
    Task<bool> RemoveVariantImageAsync(Guid variantId, string imageUrl, CancellationToken cancellationToken = default);
    
    // Multi-image variant management
    Task<ApiResponse<List<ProductVariantResponse>>> AddVariantImagesAsync(Guid variantId, List<string> imageUrls, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> BulkUploadImagesByColorAsync(Guid productId, string colorValue, List<string> imageUrls, string colorAttributeKey = "Color", CancellationToken cancellationToken = default);
    Task<ApiResponse<ProductVariantResponse>> ReorderVariantImagesAsync(Guid variantId, List<string> orderedImageUrls, CancellationToken cancellationToken = default);
    
    // Admin-specific methods
    Task<PagedResult<ProductResponse>> GetProductsWithFiltersAsync(ProductFilterRequest filter, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AdjustStockAsync(Guid productId, int quantityChange, string reason, Guid? variantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductResponse>> GetLowStockProductsAsync(int threshold = 10, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ProductVariantResponse>>> CreateProductVariantsBulkAsync(Guid productId, List<CreateProductVariantRequest> variants, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockAuditLogDto>> GetStockAuditLogsAsync(Guid productId, CancellationToken cancellationToken = default);
}