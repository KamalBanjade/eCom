using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Orders;

namespace Commerce.Domain.Entities.Payments;

/// <summary>
/// Audit log for payment verification and reconciliation
/// </summary>
public class PaymentAuditLog : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public string? Pidx { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;  // JSON from Khalti
    public DateTime CheckedAt { get; set; }
}
