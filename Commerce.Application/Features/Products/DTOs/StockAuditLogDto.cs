namespace Commerce.Application.Features.Products.DTOs;

/// <summary>
/// DTO for stock audit log entries
/// </summary>
public class StockAuditLogDto
{
    public Guid Id { get; set; }
    public Guid ProductVariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int QuantityChanged { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    public string? UserId { get; set; }
    public Guid? ReservationId { get; set; }
    public string? Reason { get; set; }
    public DateTime Timestamp { get; set; }
}
