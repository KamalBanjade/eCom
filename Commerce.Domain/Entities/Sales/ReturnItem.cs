using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;

namespace Commerce.Domain.Entities.Sales;

public class ReturnItem : BaseEntity
{
    public Guid ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;

    public Guid OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? AdminNotes { get; set; }
    public ReturnItemStatus Status { get; set; } 

    public decimal UnitPrice { get; set; }
    public bool IsRestocked { get; set; }
    public DateTime? RestockedAt { get; set; }
    
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    
    public decimal SubTotal => Quantity * UnitPrice;
}
