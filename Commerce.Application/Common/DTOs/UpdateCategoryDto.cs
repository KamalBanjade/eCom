namespace Commerce.Application.Common.DTOs;

public record UpdateCategoryDto(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive
);
