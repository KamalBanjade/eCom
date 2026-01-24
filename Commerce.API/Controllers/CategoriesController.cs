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
            filter: null, 
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
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<Category>>> Create(CreateCategoryDto dto, CancellationToken cancellationToken)
    {
        // 1. Validate DTO
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(ApiResponse<Category>.ErrorResponse("Category name is required."));
        }

        // 2. Check for duplicate name
        var existingCategories = await _categoryRepository.GetAsync(
            filter: c => c.Name.ToLower() == dto.Name.ToLower(), 
            ct: cancellationToken
        );
        
        if (existingCategories.Any())
        {
            return Conflict(ApiResponse<Category>.ErrorResponse($"Category with name '{dto.Name}' already exists."));
        }

        // 3. Determine Display Order
        int displayOrder = dto.DisplayOrder ?? 0;
        if (!dto.DisplayOrder.HasValue || dto.DisplayOrder.Value == 0)
        {
            var activeCategories = await _categoryRepository.GetAsync(
                filter: c => c.IsActive, 
                ct: cancellationToken
            );
            // If explicit 0 wasn't passed or logic demands automatic from existing count
            displayOrder = activeCategories.Any() ? activeCategories.Max(c => c.DisplayOrder) + 1 : 1;
        }
        else if (dto.IsActive)
        {
            // User provided a specific order, and the category is active.
            // We need to shift existing active categories down if they occupy this spot or below.
            var categoriesToShift = await _categoryRepository.GetAsync(
                filter: c => c.IsActive && c.DisplayOrder >= displayOrder, 
                ct: cancellationToken
            );

            foreach (var cat in categoriesToShift)
            {
                cat.DisplayOrder++;
                await _categoryRepository.UpdateAsync(cat, cancellationToken);
            }
        }

        // 4. Map DTO to Entity
        var category = new Category
        {
            Name = dto.Name.Trim(),
            Slug = GenerateSlug(dto.Name),
            Description = dto.Description,
            DisplayOrder = displayOrder,
            IsActive = dto.IsActive
        };

        // 5. Persistence
        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ApiResponse<Category>.SuccessResponse(category));
    }

    private static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) 
            return string.Empty;

        return name.Trim().ToLower()
            .Replace(" ", "-")
            .Replace("--", "-"); 
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<Category>>> Update(Guid id, UpdateCategoryDto dto, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            return NotFound(ApiResponse<Category>.ErrorResponse("Category not found"));
        }

        // Handle Status Change Reordering
        if (category.IsActive != dto.IsActive)
        {
            // If becoming Inactive: Shift subsequent ACTIVE categories DOWN (DisplayOrder - 1)
            if (!dto.IsActive)
            {
                var categoriesToShift = await _categoryRepository.GetAsync(
                    filter: c => c.IsActive && c.DisplayOrder > category.DisplayOrder,
                    ct: cancellationToken
                );

                foreach (var cat in categoriesToShift)
                {
                    cat.DisplayOrder--;
                    await _categoryRepository.UpdateAsync(cat, cancellationToken);
                }
            }
            // If becoming Active: Shift subsequent ACTIVE categories UP (DisplayOrder + 1)
            else
            {
                // We assume we want to restore it to 'dto.DisplayOrder' (or its old one if not passed, but DTO usually has it)
                // But wait, if we are restoring, we need to make space at 'category.DisplayOrder'.
                // Any active category currently at or above this spot needs to move up.
                int targetOrder = dto.DisplayOrder; 
                // If the DTO sends a new order, use that. If it sends the OLD order (which is likely if the UI didn't change it), use that.
                
                var categoriesToShift = await _categoryRepository.GetAsync(
                    filter: c => c.IsActive && c.DisplayOrder >= targetOrder,
                    ct: cancellationToken
                );

                foreach (var cat in categoriesToShift)
                {
                    cat.DisplayOrder++;
                    await _categoryRepository.UpdateAsync(cat, cancellationToken);
                }
            }
        }

        // Check for duplicate name
        if (!string.Equals(category.Name, dto.Name, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _categoryRepository.GetAsync(
                filter: c => c.Name.ToLower() == dto.Name.ToLower() && c.Id != id, 
                ct: cancellationToken
            );

            if (duplicate.Any())
            {
                return Conflict(ApiResponse<Category>.ErrorResponse($"Category with name '{dto.Name}' already exists."));
            }

            category.Name = dto.Name.Trim();
            category.Slug = GenerateSlug(dto.Name);
        }

        category.Description = dto.Description;
        category.DisplayOrder = dto.DisplayOrder;
        category.IsActive = dto.IsActive;

        await _categoryRepository.UpdateAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<Category>.SuccessResponse(category));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Include products to check dependencies
        var categories = await _categoryRepository.GetAsync(
            filter: c => c.Id == id,
            ct: cancellationToken,
            includes: [c => c.Products]
        );
        
        var category = categories.FirstOrDefault();

        if (category == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Category not found"));
        }

        if (category.Products.Any())
        {
            return Conflict(ApiResponse<bool>.ErrorResponse($"Cannot delete category containing {category.Products.Count} products. Please remove or reassign them first."));
        }

        // Capture order for implementation of shifting
        int deletedOrder = category.DisplayOrder;
        bool wasActive = category.IsActive;

        // Hard delete first to avoid any unique constraint collisions on DisplayOrder
        await _categoryRepository.DeleteAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        // Check if we need to reorder subsequent categories (fill the gap)
        if (wasActive)
        {
             var categoriesToShift = await _categoryRepository.GetAsync(
                filter: c => c.IsActive && c.DisplayOrder > deletedOrder,
                ct: cancellationToken
            );

            if (categoriesToShift.Any())
            {
                foreach (var cat in categoriesToShift)
                {
                    cat.DisplayOrder--;
                    await _categoryRepository.UpdateAsync(cat, cancellationToken);
                }
                await _categoryRepository.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Category deleted successfully"));
    }
}
