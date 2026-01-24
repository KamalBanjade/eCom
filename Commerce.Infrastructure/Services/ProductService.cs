// File: Commerce.Infrastructure/Services/ProductService.cs
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Products;
using Commerce.Application.Features.Products.DTOs;
using Commerce.Application.Features.Inventory;
using Commerce.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore;
using Commerce.Infrastructure.Data; // Added for CommerceDbContext

namespace Commerce.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<ProductVariant> _variantRepository;
    private readonly IRepository<Category> _categoryRepository; // Added for Category validation
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryService _inventoryService;

    private readonly CommerceDbContext _context;

    public ProductService(
        IRepository<Product> productRepository,
        IRepository<ProductVariant> variantRepository,
        IRepository<Category> categoryRepository,
        IUnitOfWork unitOfWork,
        CommerceDbContext context,
        IInventoryService inventoryService)
    {
        _productRepository = productRepository;
        _variantRepository = variantRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _inventoryService = inventoryService;
    }

    // ==================== Product CRUD ====================

    public async Task<IEnumerable<ProductResponse>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        // Include Category and Variants
        var products = await _productRepository.GetAllAsync(
            cancellationToken,
            p => p.Category,
            p => p.Variants);

        var responses = new List<ProductResponse>();
        foreach (var product in products)
        {
            responses.Add(await MapToProductResponseAsync(product, cancellationToken));
        }
        return responses;
    }

    public async Task<ProductResponse?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(
            id, 
            cancellationToken,
            p => p.Category,
            p => p.Variants);

        return product is null ? null : await MapToProductResponseAsync(product, cancellationToken);
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
        return await MapToProductResponseAsync(product, cancellationToken);
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

        return await MapToProductResponseAsync(product, cancellationToken);
    }

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Soft delete logic (assuming BaseEntity or Global Filter handles IsDeleted, or we just set IsActive = false)
        // Requirements said "DeleteProductAsync (soft delete)". 
        // If we strictly follow the repository pattern's Delete method, it usually does hard delete unless configured otherwise.
        // For safe measure, let's implement soft delete by setting IsActive = false if we don't have a soft-delete mechanism in Repo.
        // Actually, let's use the Repository's Delete if it supports it, but checking the requirement implies logical delete.
        // Given IRepository usually has Delete(entity), let's check if we should just toggle IsActive.
        
        // Include variants to soft delete them as well
        var product = await _productRepository.GetByIdAsync(id, cancellationToken, p => p.Variants);
        if (product == null) return false;

        product.IsActive = false; // Soft delete by deactivating
        
        // Cascade soft delete to variants
        foreach (var variant in product.Variants)
        {
            variant.IsActive = false;
        }

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
            StockQuantity = 0, // Start at 0, then adjust
            Attributes = request.Attributes ?? new Dictionary<string, string>(),
            IsActive = true
        };

        // Ensure SKU is unique (smart generation)
        variant.SKU = await GenerateUniqueSkuAsync(request.SKU, new List<string>(), cancellationToken);

        await _variantRepository.AddAsync(variant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 🔥 LOG INITIAL STOCK AS ADJUSTMENT
        if (request.StockQuantity > 0)
        {
            await _inventoryService.AdjustStockAsync(
                variant.Id,
                request.StockQuantity,
                "Initial stock on creation",
                null,
                cancellationToken);
        }

        return await MapToVariantResponseAsync(variant, cancellationToken);
    }

    public async Task<ProductVariantResponse?> GetVariantByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(id, cancellationToken);
        return variant is null ? null : await MapToVariantResponseAsync(variant, cancellationToken);
    }

    public async Task<ProductVariantResponse?> UpdateVariantAsync(Guid id, UpdateProductVariantRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(id, cancellationToken);
        if (variant == null) return null;

        // Calculate stock delta if changed
        int stockDelta = request.StockQuantity - variant.StockQuantity;
        
        variant.SKU = request.SKU;
        variant.Price = request.Price;
        variant.DiscountPrice = request.DiscountPrice;
        // variant.StockQuantity is updated via AdjustStockAsync below to ensure locking/logging
        
        variant.Attributes = request.Attributes ?? new Dictionary<string, string>();
        variant.IsActive = request.IsActive;

        if (stockDelta != 0)
        {
            await _inventoryService.AdjustStockAsync(
                variant.Id,
                stockDelta,
                "Manual Edit via Variant Update",
                null,
                cancellationToken);
        }
        else
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await MapToVariantResponseAsync(variant, cancellationToken);
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
        var responses = new List<ProductVariantResponse>();
        foreach (var variant in variants)
        {
            responses.Add(await MapToVariantResponseAsync(variant, cancellationToken));
        }
        return responses;
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

        // Add to ImageUrls array if not already present
        if (!variant.ImageUrls.Contains(imageUrl))
            variant.ImageUrls.Add(imageUrl);
            
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveVariantImageAsync(Guid variantId, string imageUrl, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(variantId, cancellationToken);
        if (variant == null) return false;

        // Remove specific image from array
        variant.ImageUrls.Remove(imageUrl);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ==================== Admin Methods ====================

    public async Task<PagedResult<ProductResponse>> GetProductsWithFiltersAsync(ProductFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .AsNoTracking();

        if (filter.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

        if (!string.IsNullOrEmpty(filter.SearchTerm))
            query = query.Where(p => p.Name.Contains(filter.SearchTerm) || p.Brand != null && p.Brand.Contains(filter.SearchTerm));

        if (filter.IsActive.HasValue)
            query = query.Where(p => p.IsActive == filter.IsActive.Value);

        if (filter.CreatedAfter.HasValue)
            query = query.Where(p => p.CreatedAt >= filter.CreatedAfter.Value);

        if (filter.CreatedBefore.HasValue)
            query = query.Where(p => p.CreatedAt <= filter.CreatedBefore.Value);

        // Stock filtering is complex because it's on variants.
        // We'll filter products where ANY variant matches the stock criteria
        if (filter.MinStock.HasValue)
            query = query.Where(p => p.Variants.Any(v => v.StockQuantity >= filter.MinStock.Value));

        if (filter.MaxStock.HasValue)
            query = query.Where(p => p.Variants.Any(v => v.StockQuantity <= filter.MaxStock.Value));

        var totalCount = await query.CountAsync(cancellationToken);
        
        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var responses = new List<ProductResponse>();
        foreach (var product in products)
        {
            responses.Add(await MapToProductResponseAsync(product, cancellationToken));
        }
        
        return new PagedResult<ProductResponse>(responses, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<ApiResponse<bool>> AdjustStockAsync(Guid productId, int quantityChange, string reason, Guid? variantId = null, CancellationToken cancellationToken = default)
    {
        // Logic: Since stock is on Variants, we need to know WHICH variant.
        // If the interface says productId, we assume the product has a Single variant (common for simple products).
        // If multiple variants exist, we need variantId.
        
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken, p => p.Variants);
        if (product == null) return ApiResponse<bool>.ErrorResponse("Product not found");

        if (product.Variants.Count == 0)
             return ApiResponse<bool>.ErrorResponse("Product has no variants to adjust stock for");

        ProductVariant variant;

        if (variantId.HasValue)
        {
             variant = product.Variants.FirstOrDefault(v => v.Id == variantId.Value);
             if (variant == null)
                 return ApiResponse<bool>.ErrorResponse($"Variant with ID {variantId} not found in this product.");
        }
        else
        {
            if (product.Variants.Count > 1)
                 return ApiResponse<bool>.ErrorResponse("Product has multiple variants. Please adjust stock for specific variant.");
            
            variant = product.Variants.First();
        }
        
        // 🔥 PROFESSIONALLY ROUTE THROUGH INVENTORY SERVICE
        // This handles locking, validation, and audit logging atomically.
        try
        {
            await _inventoryService.AdjustStockAsync(
                variant.Id, 
                quantityChange, 
                reason, 
                null, // Could be enhanced to pass the current Admin User ID
                cancellationToken);
            
            return ApiResponse<bool>.SuccessResponse(true, "Stock adjusted successfully and logged.");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.ErrorResponse(ex.Message);
        }
    }

    public async Task<IEnumerable<ProductResponse>> GetLowStockProductsAsync(int threshold = 10, CancellationToken cancellationToken = default)
    {
        // Find products where ANY variant is low stock
        var products = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Where(p => p.Variants.Any(v => v.StockQuantity <= threshold))
            .ToListAsync(cancellationToken);

        var responses = new List<ProductResponse>();
        foreach (var product in products)
        {
            responses.Add(await MapToProductResponseAsync(product, cancellationToken));
        }
        return responses;
    }

    public async Task<ApiResponse<List<ProductVariantResponse>>> CreateProductVariantsBulkAsync(Guid productId, List<CreateProductVariantRequest> variants, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product == null)
            return ApiResponse<List<ProductVariantResponse>>.ErrorResponse("Product not found");

        var createdVariants = new List<ProductVariant>();

        // Check for duplicate SKUs in the request itself
        // We will handle duplicates during generation by checking against the list of SKUs being created
        
        // Remove bulk DB check as we handle per-item uniqueness
        var reservedSkus = new HashSet<string>();

        foreach (var request in variants)
        {
            var uniqueSku = await GenerateUniqueSkuAsync(request.SKU, reservedSkus, cancellationToken);
            reservedSkus.Add(uniqueSku);

            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SKU = uniqueSku,
                Price = request.Price,
                DiscountPrice = request.DiscountPrice,
                StockQuantity = request.StockQuantity,
                Attributes = request.Attributes ?? new Dictionary<string, string>(),
                IsActive = true
            };
            createdVariants.Add(variant);
            await _variantRepository.AddAsync(variant, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
             // Fallback for race conditions
             if (ex.InnerException?.Message.Contains("duplicate key") == true || ex.Message.Contains("duplicate key"))
                return ApiResponse<List<ProductVariantResponse>>.ErrorResponse("A variant with one of these SKUs already exists.");
             throw;
        }

        var variantResponses = new List<ProductVariantResponse>();
        foreach (var variant in createdVariants)
        {
            variantResponses.Add(await MapToVariantResponseAsync(variant, cancellationToken));
        }
        return ApiResponse<List<ProductVariantResponse>>.SuccessResponse(
            variantResponses, 
            $"{createdVariants.Count} variants created successfully");
    }

    // ==================== Mappers ====================

    private async Task<ProductResponse> MapToProductResponseAsync(Product product, CancellationToken cancellationToken = default)
    {
        var variantResponses = new List<ProductVariantResponse>();
        if (product.Variants != null)
        {
            foreach (var variant in product.Variants)
            {
                variantResponses.Add(await MapToVariantResponseAsync(variant, cancellationToken));
            }
        }

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
            product.ImageUrls ?? new List<string>(),
            variantResponses
        );
    }

    private async Task<ProductVariantResponse> MapToVariantResponseAsync(ProductVariant variant, CancellationToken cancellationToken = default)
    {
        int availableStock = await _inventoryService.GetAvailableStockAsync(variant.Id, cancellationToken);
        
        return new ProductVariantResponse(
            variant.Id,
            variant.ProductId,
            variant.SKU,
            variant.Price,
            variant.DiscountPrice,
            variant.StockQuantity,
            availableStock,
            variant.Attributes ?? new Dictionary<string, string>(),
            variant.IsActive,
            variant.ImageUrls
        );
    }
    

    private async Task<string> GenerateUniqueSkuAsync(string baseSku, IEnumerable<string> reservedSkus, CancellationToken cancellationToken)
    {
        // Helper to check availability
        async Task<bool> IsSkuTaken(string sku)
        {
            if (reservedSkus.Contains(sku)) return true;
            var exists = await _variantRepository.GetAsync(v => v.SKU == sku, cancellationToken);
            return exists.Any();
        }

        // 1. Check exact match
        if (!await IsSkuTaken(baseSku)) return baseSku;

        // 2. Parse to find prefix
        string prefix = baseSku;
        int counter = 1;

        // Check if it already has a numeric suffix like "-01"
        var match = System.Text.RegularExpressions.Regex.Match(baseSku, @"^(.*)-(\d+)$");
        if (match.Success)
        {
            prefix = match.Groups[1].Value;
            if (int.TryParse(match.Groups[2].Value, out int existingCounter))
            {
                counter = existingCounter + 1;
            }
        }

        // 3. Loop until unique
        while (true)
        {
            var candidateSku = $"{prefix}-{counter:D2}";
            if (!await IsSkuTaken(candidateSku)) return candidateSku;
            counter++;
        }
    }
    
    // ==========================================
    // Multi-Image Variant Management
    // ==========================================
    
    public async Task<ApiResponse<List<ProductVariantResponse>>> AddVariantImagesAsync(
        Guid variantId, 
        List<string> imageUrls, 
        CancellationToken cancellationToken = default)
    {
        if (imageUrls == null || imageUrls.Count == 0)
            return ApiResponse<List<ProductVariantResponse>>.ErrorResponse("No image URLs provided");
            
        var variant = await _variantRepository.GetByIdAsync(variantId, cancellationToken);
        if (variant == null)
            return ApiResponse<List<ProductVariantResponse>>.ErrorResponse("Variant not found");
        
        // Add new images to existing list (max 10 total)
        foreach (var url in imageUrls)
        {
            if (variant.ImageUrls.Count >= 10)
            {
                return ApiResponse<List<ProductVariantResponse>>.ErrorResponse("Maximum 10 images per variant");
            }
            
            if (!variant.ImageUrls.Contains(url))
            {
                variant.ImageUrls.Add(url);
            }
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return ApiResponse<List<ProductVariantResponse>>.SuccessResponse(
            new List<ProductVariantResponse> { await MapToVariantResponseAsync(variant, cancellationToken) },
            $"Added {imageUrls.Count} image(s) to variant"
        );
    }
    
    public async Task<ApiResponse<int>> BulkUploadImagesByColorAsync(
        Guid productId,
        string colorValue,
        List<string> imageUrls,
        string colorAttributeKey = "Color",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(colorValue))
            return ApiResponse<int>.ErrorResponse("Color value is required");
            
        if (imageUrls == null || imageUrls.Count == 0)
            return ApiResponse<int>.ErrorResponse("No image URLs provided");
        
        if (imageUrls.Count > 10)
            return ApiResponse<int>.ErrorResponse("Maximum 10 images per color");
        
        // Find all variants of this product with matching color
        var allVariants = await _variantRepository.GetAsync(
            v => v.ProductId == productId,
            cancellationToken
        );
        
        var matchingVariants = allVariants.Where(v =>
        {
            if (v.Attributes == null) return false;
            
            // Case-insensitive search for color attribute key
            var colorEntry = v.Attributes.FirstOrDefault(a =>
                a.Key.Equals(colorAttributeKey, StringComparison.OrdinalIgnoreCase)
            );
            
            return colorEntry.Value != null && 
                   colorEntry.Value.Equals(colorValue, StringComparison.OrdinalIgnoreCase);
        }).ToList();
        
        if (!matchingVariants.Any())
            return ApiResponse<int>.ErrorResponse($"No variants found with {colorAttributeKey}='{colorValue}'");
        
        // Apply same images to all matching variants
        foreach (var variant in matchingVariants)
        {
            // Append new images (up to 10 total)
            foreach (var url in imageUrls)
            {
                if (variant.ImageUrls.Count >= 10) break;
                
                if (!variant.ImageUrls.Contains(url))
                {
                    variant.ImageUrls.Add(url);
                }
            }
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return ApiResponse<int>.SuccessResponse(
            matchingVariants.Count,
            $"Applied {imageUrls.Count} image(s) to {matchingVariants.Count} {colorValue} variant(s)"
        );
    }
    
    public async Task<ApiResponse<ProductVariantResponse>> ReorderVariantImagesAsync(
        Guid variantId,
        List<string> orderedImageUrls,
        CancellationToken cancellationToken = default)
    {
        if (orderedImageUrls == null || orderedImageUrls.Count == 0)
            return ApiResponse<ProductVariantResponse>.ErrorResponse("No image URLs provided");
            
        var variant = await _variantRepository.GetByIdAsync(variantId, cancellationToken);
        if (variant == null)
            return ApiResponse<ProductVariantResponse>.ErrorResponse("Variant not found");
        
        // Validate that all provided URLs exist in current images
        var currentUrls = variant.ImageUrls.ToHashSet();
        var providedUrls = orderedImageUrls.ToHashSet();
        
        if (!providedUrls.IsSubsetOf(currentUrls))
            return ApiResponse<ProductVariantResponse>.ErrorResponse("Some URLs do not belong to this variant");
        
        // Update with new order
        variant.ImageUrls = orderedImageUrls;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return ApiResponse<ProductVariantResponse>.SuccessResponse(
            await MapToVariantResponseAsync(variant, cancellationToken),
            "Image order updated successfully"
        );
    }

    public async Task<IEnumerable<StockAuditLogDto>> GetStockAuditLogsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        // Get all variant IDs for this product
        var variantIds = await _context.ProductVariants
            .Where(v => v.ProductId == productId)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        if (!variantIds.Any())
            return Enumerable.Empty<StockAuditLogDto>();

        // Get all stock audit logs for these variants
        var logs = await _context.Set<Commerce.Domain.Entities.Inventory.StockAuditLog>()
            .Where(log => variantIds.Contains(log.ProductVariantId))
            .OrderByDescending(log => log.Timestamp)
            .Take(100) // Limit to last 100 entries
            .ToListAsync(cancellationToken);

        // Get variant names for mapping
        var variants = await _context.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.SKU, cancellationToken);

        return logs.Select(log => new StockAuditLogDto
        {
            Id = log.Id,
            ProductVariantId = log.ProductVariantId,
            VariantName = variants.GetValueOrDefault(log.ProductVariantId, "Unknown"),
            Action = log.Action,
            QuantityChanged = log.QuantityChanged,
            StockBefore = log.StockBefore,
            StockAfter = log.StockAfter,
            UserId = log.UserId,
            ReservationId = log.ReservationId,
            Reason = log.Reason,
            Timestamp = log.Timestamp
        });
    }
    }
    
