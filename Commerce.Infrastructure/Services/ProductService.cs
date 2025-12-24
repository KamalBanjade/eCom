// File: Commerce.Infrastructure/Services/ProductService.cs
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Products;
using Commerce.Application.Features.Products.DTOs;
using Commerce.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore; // For Include syntax
using System.Linq.Expressions;

namespace Commerce.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<ProductVariant> _variantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
    IRepository<Product> productRepository,
    IRepository<ProductVariant> variantRepository,
    IUnitOfWork unitOfWork)  // ← Add this parameter
{
    _productRepository = productRepository;
    _variantRepository = variantRepository;
    _unitOfWork = unitOfWork;
}
    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(
            cancellationToken,
            p => p.Variants); // Eager load Variants

        return products.Select(MapToProductDto);
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(
            id,
            cancellationToken,
            p => p.Variants); // Eager load Variants

        return product is null ? null : MapToProductDto(product);
    }

 public async Task<ProductDto> CreateProductAsync(
    string name,
    string description,
    decimal price,
    CancellationToken cancellationToken = default)
{
    var product = new Product
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = description,
        BasePrice = price,
        Category = "General",
        Brand = null,
        IsActive = true,
        Variants = new List<ProductVariant>()
    };

    await _productRepository.AddAsync(product, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);  // ← Save to DB

    return MapToProductDto(product);
}

    public async Task<ProductVariantDto> CreateProductVariantAsync(
    Guid productId,
    string sku,
    decimal price,
    Dictionary<string, string> attributes,
    CancellationToken cancellationToken = default)
{
    var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
    if (product == null)
        throw new KeyNotFoundException($"Product with ID {productId} not found.");

    var variant = new ProductVariant
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        SKU = sku,
        Price = price,
        Attributes = attributes ?? new Dictionary<string, string>(),
        AvailableStock = 0
    };

    await _variantRepository.AddAsync(variant, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);  // ← Save to DB

    return MapToVariantDto(variant);
}

public async Task<ProductVariantDto?> GetProductVariantByIdAsync(Guid id, CancellationToken cancellationToken = default)
{
    var variant = await _variantRepository.GetByIdAsync(id, cancellationToken);
    return variant is null ? null : MapToVariantDto(variant);
}

public async Task<IEnumerable<ProductVariantDto>> GetVariantsByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
{
    var variants = await _variantRepository.GetAsync(v => v.ProductId == productId);

    return variants.Select(MapToVariantDto);
}

    // ==================== Private Mapping Methods ====================

    private static ProductDto MapToProductDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            BasePrice = product.BasePrice,
            Category = product.Category,
            Brand = product.Brand,
            IsActive = product.IsActive,
            Variants = product.Variants?
                .Select(MapToVariantDto)
                .ToList() ?? new List<ProductVariantDto>()
        };
    }

    private static ProductVariantDto MapToVariantDto(ProductVariant variant)
    {
        return new ProductVariantDto
        {
            Id = variant.Id,
            SKU = variant.SKU,
            Price = variant.Price,
            Attributes = variant.Attributes ?? new Dictionary<string, string>(),
            AvailableStock = variant.AvailableStock
        };
    }
}