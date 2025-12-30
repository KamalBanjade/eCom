using Commerce.Domain.Enums;

namespace Commerce.Application.Features.Products.DTOs;

/// <summary>
/// Filter request for products with enhanced admin filtering
/// </summary>
public class ProductFilterRequest
{
    public Guid? CategoryId { get; set; }
    public string? SearchTerm { get; set; }
    public int? MinStock { get; set; }
    public int? MaxStock { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Request to adjust product stock
/// </summary>
public class AdjustStockRequest
{
    public int QuantityChange { get; set; } // Positive for increase, negative for decrease
    public string Reason { get; set; } = string.Empty;
}
