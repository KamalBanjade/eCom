namespace Commerce.Application.Common.DTOs;

public record CreateCategoryDto(
    string Name,
    string? Description,
    int? DisplayOrder,
    bool IsActive
);
