using Commerce.Domain.Entities.Base;
using Commerce.Domain.Entities.Users;
using Commerce.Domain.Enums;
using Commerce.Domain.ValueObjects;

namespace Commerce.Domain.Entities.Orders;

/// <summary>
/// Order aggregate root - immutable after placement
/// </summary>
public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Addresses stored as JSON
    public Address ShippingAddress { get; set; } = null!;
    public Address BillingAddress { get; set; } = null!;
    
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
