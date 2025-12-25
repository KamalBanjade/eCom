// File: Commerce.Infrastructure/Services/ProductService.cs
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Products;
using Commerce.Application.Features.Products.DTOs;
using Commerce.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<ProductVariant> _variantRepository;
    private readonly IRepository<Category> _categoryRepository; // Added for Category validation
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IRepository<Product> productRepository,
        IRepository<ProductVariant> variantRepository,
        IRepository<Category> categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _variantRepository = variantRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    // ==================== Product CRUD ====================

    public async Task<IEnumerable<ProductResponse>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        // Include Category and Variants
        var products = await _productRepository.GetAllAsync(
            cancellationToken,
            p => p.Category,
            p => p.Variants);

        return products.Select(MapToProductResponse);
    }

    public async Task<ProductResponse?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(
            id, 
            cancellationToken,
            p => p.Category,
            p => p.Variants);

        return product is null ? null : MapToProductResponse(product);
    }

    public async Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        // Validate Category
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category == null)
            throw new KeyNotFoundException($"Category with ID {request.CategoryId} not found.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            BasePrice = request.BasePrice,
            CategoryId = request.CategoryId,
            Brand = request.Brand,
            IsActive = true,
            Variants = new List<ProductVariant>()
        };

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch again to ensure Category is loaded for response mapping, or map manually
        product.Category = category; 
        return MapToProductResponse(product);
    }

    public async Task<ProductResponse?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken, p => p.Variants, p => p.Category);
        if (product == null) return null;

        if (product.CategoryId != request.CategoryId)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
            if (category == null)
                throw new KeyNotFoundException($"Category with ID {request.CategoryId} not found.");
            product.CategoryId = request.CategoryId;
            product.Category = category;
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.BasePrice = request.BasePrice;
        product.Brand = request.Brand;
        product.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToProductResponse(product);
    }

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Soft delete logic (assuming BaseEntity or Global Filter handles IsDeleted, or we just set IsActive = false)
        // Requirements said "DeleteProductAsync (soft delete)". 
        // If we strictly follow the repository pattern's Delete method, it usually does hard delete unless configured otherwise.
        // For safe measure, let's implement soft delete by setting IsActive = false if we don't have a soft-delete mechanism in Repo.
        // Actually, let's use the Repository's Delete if it supports it, but checking the requirement implies logical delete.
        // Given IRepository usually has Delete(entity), let's check if we should just toggle IsActive.
        
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null) return false;

        product.IsActive = false; // Soft delete by deactivating
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ==================== Variant CRUD ====================

    public async Task<ProductVariantResponse> CreateVariantAsync(Guid productId, CreateProductVariantRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product == null)
            throw new KeyNotFoundException($"Product with ID {productId} not found.");

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SKU = request.SKU,
            Price = request.Price,
            DiscountPrice = request.DiscountPrice,
            StockQuantity = request.StockQuantity,
            Attributes = request.Attributes ?? new Dictionary<string, string>(),
            IsActive = true
        };

        await _variantRepository.AddAsync(variant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToVariantResponse(variant);
    }

    public async Task<ProductVariantResponse?> GetVariantByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(id, cancellationToken);
        return variant is null ? null : MapToVariantResponse(variant);
    }

    public async Task<ProductVariantResponse?> UpdateVariantAsync(Guid id, UpdateProductVariantRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(id, cancellationToken);
        if (variant == null) return null;

        variant.SKU = request.SKU;
        variant.Price = request.Price;
        variant.DiscountPrice = request.DiscountPrice;
        variant.StockQuantity = request.StockQuantity;
        variant.Attributes = request.Attributes ?? new Dictionary<string, string>();
        variant.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToVariantResponse(variant);
    }

    public async Task<bool> DeleteVariantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(id, cancellationToken);
        if (variant == null) return false;

        variant.IsActive = false; // Soft delete
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<ProductVariantResponse>> GetVariantsByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var variants = await _variantRepository.GetAsync(v => v.ProductId == productId);
        return variants.Select(MapToVariantResponse);
    }

    // ==================== Category ====================
    public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        
        return categories.Select(c => new CategoryDto(
        c.Id,
        c.Name,
        c.Description ?? string.Empty,
        c.IsActive
));
    }

    // ==================== Image Management ====================

    public async Task<bool> AddProductImagesAsync(Guid productId, IEnumerable<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product == null) return false;

        if (product.ImageUrls == null)
            product.ImageUrls = new List<string>();

        foreach (var url in imageUrls)
        {
            if (!product.ImageUrls.Contains(url))
                product.ImageUrls.Add(url);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveProductImageAsync(Guid productId, string imageUrl, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product == null) return false;

        if (product.ImageUrls != null && product.ImageUrls.Contains(imageUrl))
        {
            product.ImageUrls.Remove(imageUrl);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> UpdateVariantImageAsync(Guid variantId, string imageUrl, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(variantId, cancellationToken);
        if (variant == null) return false;

        variant.ImageUrl = imageUrl;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveVariantImageAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(variantId, cancellationToken);
        if (variant == null) return false;

        variant.ImageUrl = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ==================== Mappers ====================

    private static ProductResponse MapToProductResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.BasePrice,
            product.Category?.Name ?? "Unknown", // Handle null if not included, though we try to include it
            product.CategoryId,
            product.Brand,
            product.IsActive,
            product.CreatedAt,
            product.Variants?.Select(MapToVariantResponse).ToList() ?? new List<ProductVariantResponse>()
        );
    }

    private static ProductVariantResponse MapToVariantResponse(ProductVariant variant)
    {
        return new ProductVariantResponse(
            variant.Id,
            variant.ProductId,
            variant.SKU,
            variant.Price,
            variant.DiscountPrice,
            variant.StockQuantity,
            variant.Attributes ?? new Dictionary<string, string>(),
            variant.IsActive
        );
    }
}