using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;
using Commerce.Domain.ValueObjects;

namespace Commerce.Application.Features.Orders.DTOs;

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    
    public string? AppliedCouponCode { get; set; }
    
    public Address? ShippingAddress { get; set; }
    public Address? BillingAddress { get; set; }
    
    public string? PaymentUrl { get; set; }  // Khalti payment URL for redirect
    
    // Assignment tracking
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserEmail { get; set; }
    public string? AssignedRole { get; set; }
    public DateTime? AssignedAt { get; set; }
    
    // Customer info
    public string? CustomerEmail { get; set; }
    public string? CustomerName { get; set; }
    
    public List<OrderItemDto> Items { get; set; } = new();
    
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

public class PlaceOrderRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public int ShippingAddressIndex { get; set; } // Index in CustomerProfile.ShippingAddresses list
    public int? BillingAddressIndex { get; set; } // If null, use shipping address
}

public class OrderFilterRequest
{
    public OrderStatus? Status { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? UserId { get; set; }
    public string? OrderNumber { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}

public class AssignOrderRequest
{
    public Guid AssignedToUserId { get; set; }
    public string AssignedRole { get; set; } = string.Empty; // "Warehouse" or "Support"
}
