// File: Commerce.API/Controllers/ProductsController.cs
using Commerce.Application.Features.Products;
using Commerce.Application.Features.Products.DTOs;
using Commerce.Application.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllProductsAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<ProductDto>>.SuccessResponse(products));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        if (product == null)
            return NotFound(ApiResponse<ProductDto>.ErrorResponse("Product not found"));

        return Ok(ApiResponse<ProductDto>.SuccessResponse(product));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateProductAsync(
            request.Name,
            request.Description,
            request.Price,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            ApiResponse<ProductDto>.SuccessResponse(product, "Product created successfully"));
    }

    [HttpPost("{productId}/variants")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ProductVariantDto>>> CreateVariant(
        Guid productId,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var variant = await _productService.CreateProductVariantAsync(
            productId,
            request.SKU,
            request.Price,
            request.Attributes,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetVariantById),
            new { id = variant.Id },
            ApiResponse<ProductVariantDto>.SuccessResponse(variant, "Variant created successfully"));
    }
    [HttpGet("variants/{id}")]
[AllowAnonymous]
public async Task<ActionResult<ApiResponse<ProductVariantDto>>> GetVariantById(Guid id, CancellationToken cancellationToken)
{
    var variant = await _productService.GetProductVariantByIdAsync(id, cancellationToken);
    if (variant == null)
        return NotFound(ApiResponse<ProductVariantDto>.ErrorResponse("Variant not found"));

    return Ok(ApiResponse<ProductVariantDto>.SuccessResponse(variant));
}

    [HttpGet("{productId}/variants")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductVariantDto>>>> GetVariantsByProduct(
        Guid productId,
        CancellationToken cancellationToken)
    {
    var variants = await _productService.GetVariantsByProductIdAsync(productId, cancellationToken);

    if (!variants.Any())
    {
        return NotFound(ApiResponse<IEnumerable<ProductVariantDto>>.ErrorResponse("No variants found for this product"));
    }

    return Ok(ApiResponse<IEnumerable<ProductVariantDto>>.SuccessResponse(variants));
}
}   
public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
public class CreateProductVariantRequest
{
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}
