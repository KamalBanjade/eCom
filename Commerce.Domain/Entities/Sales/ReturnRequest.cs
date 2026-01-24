using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;

namespace Commerce.Domain.Entities.Sales;

public class ReturnRequest
{
    public Guid Id { get; set; }
    
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    // Reason moved to ReturnItem
    // public string Reason { get; set; } = string.Empty;
    public ReturnStatus ReturnStatus { get; set; }
    
    public decimal TotalRefundAmount { get; set; }
    public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
    public RefundMethod? RefundMethod { get; set; }
    
    public string? KhaltiPidx { get; set; }
    
    // Assignment tracking
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedRole { get; set; }
    public DateTime? AssignedAt { get; set; }
    
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? InspectionCompletedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}
