using Commerce.Application.Features.Products;
using Commerce.Application.Features.Products.DTOs;
using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.API.DTOs; // <-- Add this
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IImageStorageService _imageStorageService;

    public ProductsController(IProductService productService, IImageStorageService imageStorageService)
    {
        _productService = productService;
        _imageStorageService = imageStorageService;
    }

    // ==================== Products ====================

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductResponse>>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllProductsAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<ProductResponse>>.SuccessResponse(products));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        if (product == null)
            return NotFound(ApiResponse<ProductResponse>.ErrorResponse("Product not found"));

        return Ok(ApiResponse<ProductResponse>.SuccessResponse(product));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productService.CreateProductAsync(request, cancellationToken);
            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                ApiResponse<ProductResponse>.SuccessResponse(product, "Product created successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<ProductResponse>.ErrorResponse(ex.Message));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(id, request, cancellationToken);
            if (product == null)
                return NotFound(ApiResponse<ProductResponse>.ErrorResponse("Product not found"));

            return Ok(ApiResponse<ProductResponse>.SuccessResponse(product, "Product updated successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<ProductResponse>.ErrorResponse(ex.Message));
        }
    }



    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteProductAsync(id, cancellationToken);
        if (!result)
            return NotFound(ApiResponse<bool>.ErrorResponse("Product not found"));

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Product deleted successfully"));
    }

    // ==================== Categories ====================

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<CategoryDto>>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _productService.GetAllCategoriesAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<CategoryDto>>.SuccessResponse(categories));
    }

    // ==================== Product Images ====================

    /// <summary>
    /// Upload multiple product images - Admin only
    /// </summary>
    [HttpPost("{id}/images")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<List<string>>>> UploadProductImages(
        Guid id,
        [FromForm] UploadProductImagesRequest request,
        CancellationToken cancellationToken)
    {
        var files = request.Files;

        if (files == null || files.Count == 0)
            return BadRequest(ApiResponse<List<string>>.ErrorResponse("No files uploaded"));

        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        foreach (var file in files)
        {
            if (file.Length > maxFileSize)
                return BadRequest(ApiResponse<List<string>>.ErrorResponse($"File {file.FileName} exceeds 5MB limit"));

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(ApiResponse<List<string>>.ErrorResponse($"File {file.FileName} has invalid format. Allowed: JPG, PNG, WebP"));
        }

        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        if (product == null)
            return NotFound(ApiResponse<List<string>>.ErrorResponse("Product not found"));

        var uploadedUrls = new List<string>();
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var imageUrl = await _imageStorageService.UploadImageAsync(
                stream,
                file.FileName,
                $"products/{id}",
                cancellationToken
            );
            uploadedUrls.Add(imageUrl);
        }

        var dbResult = await _productService.AddProductImagesAsync(id, uploadedUrls, cancellationToken);
        if (!dbResult)
            return StatusCode(500, ApiResponse<List<string>>.ErrorResponse("Files uploaded to Cloudinary but failed to update product database."));

        return Ok(ApiResponse<List<string>>.SuccessResponse(uploadedUrls, $"{uploadedUrls.Count} image(s) uploaded successfully and saved to database"));
    }

    /// <summary>
    /// Delete product image - Admin only
    /// </summary>
    [HttpDelete("{id}/images")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProductImage(
        Guid id,
        [FromQuery] string imageUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return BadRequest(ApiResponse<bool>.ErrorResponse("Image URL is required"));

        var deleted = await _imageStorageService.DeleteImageAsync(imageUrl, cancellationToken);
        if (!deleted)
            return NotFound(ApiResponse<bool>.ErrorResponse("Image not found in storage or already deleted"));

        var dbResult = await _productService.RemoveProductImageAsync(id, imageUrl, cancellationToken);
        if (!dbResult)
            return BadRequest(ApiResponse<bool>.ErrorResponse("Image removed from storage but failed to update product database or image not found in product list"));

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Image deleted successfully from storage and database"));
    }
}