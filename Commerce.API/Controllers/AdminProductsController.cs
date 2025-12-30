using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Products;
using Commerce.Application.Features.Products.DTOs;
using Commerce.Domain.Entities.Products; // For ProductFilterRequest enums if needed
using Commerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/admin/products")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Warehouse")] // Warehouse can adjust stock
public class AdminProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public AdminProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Admin: List products with advanced filters (stock, status, category, date)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductResponse>>>> GetProducts(
        [FromQuery] ProductFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _productService.GetProductsWithFiltersAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<ProductResponse>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin/Warehouse: Adjust stock for a product (single variant)
    /// </summary>
    [HttpPost("{id}/adjust-stock")]
    public async Task<ActionResult<ApiResponse<bool>>> AdjustStock(
        Guid id,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.AdjustStockAsync(id, request.QuantityChange, request.Reason, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Admin/Warehouse: Get low stock products
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductResponse>>>> GetLowStockProducts(
        [FromQuery] int threshold = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetLowStockProductsAsync(threshold, cancellationToken);
        return Ok(ApiResponse<IEnumerable<ProductResponse>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Create multiple variants for a product
    /// </summary>
    [HttpPost("{id}/variants/bulk")]
    [Authorize(Roles = "Admin,SuperAdmin")] // Restrict bulk creation to Admin+
    public async Task<ActionResult<ApiResponse<List<ProductVariantResponse>>>> CreateVariantsBulk(
        Guid id,
        [FromBody] List<CreateProductVariantRequest> variants,
        CancellationToken cancellationToken)
    {
        var result = await _productService.CreateProductVariantsBulkAsync(id, variants, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return StatusCode(201, result);
    }
}
