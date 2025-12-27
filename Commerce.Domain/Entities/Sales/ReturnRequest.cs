using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;

namespace Commerce.Domain.Entities.Sales;

public class ReturnRequest
{
    public Guid Id { get; set; }
    
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public string Reason { get; set; } = string.Empty;
    public ReturnStatus ReturnStatus { get; set; }
    
    public decimal RefundAmount { get; set; }
    public RefundMethod? RefundMethod { get; set; }
    
    public string? KhaltiPidx { get; set; }
    
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}
