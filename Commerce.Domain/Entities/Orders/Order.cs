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
    
    public Guid? CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    
    public OrderStatus OrderStatus { get; set; } = OrderStatus.PendingPayment;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.NotRequired;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;
    
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Coupon snapshot
    public string? AppliedCouponCode { get; set; }
    
    // Khalti payment fields
    public string? Pidx { get; set; }           // Khalti payment identifier
    public string? PaymentUrl { get; set; }     // Khalti checkout URL
    public DateTime? PaidAt { get; set; }       // Payment completion timestamp
    
    // Addresses stored as JSON
    public Address ShippingAddress { get; set; } = null!;
    public Address BillingAddress { get; set; } = null!;
    
    // Assignment tracking for warehouse/support
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedRole { get; set; }  // "Warehouse" or "Support"
    public DateTime? AssignedAt { get; set; }
    
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
