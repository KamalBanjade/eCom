using Commerce.Application.Common.Interfaces;
using Commerce.Application.Common.DTOs;
using Commerce.Domain.Entities.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IRepository<Category> _categoryRepository;

    public CategoriesController(IRepository<Category> categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<Category>>>> GetAll(CancellationToken cancellationToken)
    {
        // Get all categories, we can add logic to filter active ones or sort by display order if needed
        var categories = await _categoryRepository.GetAsync(
            filter: c => c.IsActive, 
            ct: cancellationToken,
            includes: []);
            
        // Order by DisplayOrder in memory or we could add OrderBy to repository if supported, 
        // but for now simple list is fine. Sorting in memory for small list.
        var roundedCategories = categories.OrderBy(c => c.DisplayOrder).ToList();

        return Ok(ApiResponse<IEnumerable<Category>>.SuccessResponse(roundedCategories));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<Category>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        
        if (category == null)
            return NotFound(ApiResponse<Category>.ErrorResponse("Category not found"));

        return Ok(ApiResponse<Category>.SuccessResponse(category));
    }
}
