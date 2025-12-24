using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Products;
using Commerce.Domain.Entities.Products;
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
    public async Task<ActionResult<ApiResponse<IEnumerable<Product>>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllProductsAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<Product>>.SuccessResponse(products));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<Product>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        if (product == null)
        {
            return NotFound(ApiResponse<Product>.ErrorResponse("Product not found"));
        }
        return Ok(ApiResponse<Product>.SuccessResponse(product));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<Product>>> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _productService.CreateProductAsync(request.Name, request.Description, request.Price, cancellationToken);
        
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ApiResponse<Product>.SuccessResponse(product, "Product created successfully"));
    }
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
