using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Products.DTOs;
using Commerce.Application.Features.Products;
using Commerce.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductVariantsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IImageStorageService _imageStorageService;

    public ProductVariantsController(IProductService productService, IImageStorageService imageStorageService)
    {
        _productService = productService;
        _imageStorageService = imageStorageService;
    }

    [HttpPost("{productId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ProductVariantResponse>>> CreateVariant(
        Guid productId,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var variant = await _productService.CreateVariantAsync(productId, request, cancellationToken);
            return CreatedAtAction(
                nameof(GetVariantById),
                new { id = variant.Id },
                ApiResponse<ProductVariantResponse>.SuccessResponse(variant, "Variant created successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProductVariantResponse>.ErrorResponse(ex.Message));
        }
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductVariantResponse>>> GetVariantById(Guid id, CancellationToken cancellationToken)
    {
        var variant = await _productService.GetVariantByIdAsync(id, cancellationToken);
        if (variant == null)
            return NotFound(ApiResponse<ProductVariantResponse>.ErrorResponse("Variant not found"));

        return Ok(ApiResponse<ProductVariantResponse>.SuccessResponse(variant));
    }

    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductVariantResponse>>>> GetVariantsByProductId(Guid productId, CancellationToken cancellationToken)
    {
        var variants = await _productService.GetVariantsByProductIdAsync(productId, cancellationToken);
        return Ok(ApiResponse<IEnumerable<ProductVariantResponse>>.SuccessResponse(variants));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ProductVariantResponse>>> UpdateVariant(
        Guid id,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var variant = await _productService.UpdateVariantAsync(id, request, cancellationToken);
        if (variant == null)
            return NotFound(ApiResponse<ProductVariantResponse>.ErrorResponse("Variant not found"));

        return Ok(ApiResponse<ProductVariantResponse>.SuccessResponse(variant, "Variant updated successfully"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteVariant(Guid id, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteVariantAsync(id, cancellationToken);
        if (!result)
            return NotFound(ApiResponse<bool>.ErrorResponse("Variant not found"));

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Variant deleted successfully"));
    }

    // ==================== Images ====================

    [HttpPost("{id}/image")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<string>>> UploadVariantImage(
        Guid id,
        [FromForm] UploadVariantImageRequest request,
        CancellationToken cancellationToken)
    {
        var file = request.File;

        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<string>.ErrorResponse("No file uploaded"));

        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        if (file.Length > maxFileSize)
            return BadRequest(ApiResponse<string>.ErrorResponse("File exceeds 5MB limit"));

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(ApiResponse<string>.ErrorResponse("Invalid file format. Allowed: JPG, PNG, WebP"));

        await using var stream = file.OpenReadStream();
        var imageUrl = await _imageStorageService.UploadImageAsync(
            stream,
            file.FileName,
            $"variants/{id}",
            cancellationToken
        );

        var dbResult = await _productService.UpdateVariantImageAsync(id, imageUrl, cancellationToken);
        if (!dbResult)
            return StatusCode(500, ApiResponse<string>.ErrorResponse("File uploaded to Cloudinary but failed to update variant database."));

        return Ok(ApiResponse<string>.SuccessResponse(imageUrl, "Variant image uploaded and saved successfully"));
    }

    [HttpDelete("{id}/image")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteVariantImage(
        Guid id,
        [FromQuery] string imageUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return BadRequest(ApiResponse<bool>.ErrorResponse("Image URL is required"));

        var deleted = await _imageStorageService.DeleteImageAsync(imageUrl, cancellationToken);
        if (!deleted)
            return NotFound(ApiResponse<bool>.ErrorResponse("Image not found in storage or already deleted"));

        var dbResult = await _productService.RemoveVariantImageAsync(id, cancellationToken);
        if (!dbResult)
            return BadRequest(ApiResponse<bool>.ErrorResponse("Image removed from storage but failed to update variant database"));

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Variant image deleted successfully from storage and database"));
    }
}
