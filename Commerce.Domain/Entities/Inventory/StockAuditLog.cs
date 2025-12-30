using Commerce.Domain.Entities.Base;

namespace Commerce.Domain.Entities.Inventory;

/// <summary>
/// Audit log for all stock-related operations to maintain compliance trail
/// </summary>
public class StockAuditLog : BaseEntity
{
    public Guid ProductVariantId { get; set; }
    
    /// <summary>
    /// Action type: "Reserve", "Confirm", "Release", "Cleanup"
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    public int QuantityChanged { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    
    public string? UserId { get; set; }
    public Guid? ReservationId { get; set; }
    public string? Reason { get; set; }
    
    public DateTime Timestamp { get; set; }
}
