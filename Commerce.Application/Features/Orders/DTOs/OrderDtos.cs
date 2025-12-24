using Commerce.Domain.Entities.Orders;

namespace Commerce.Application.Features.Orders.DTOs;

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CreateOrderRequest
{
    // For now, we'll assume the cart is converted to an order
    // In a real scenario, this might contain specific items or shipping method selection
    public Guid? CustomerProfileId { get; set; } // Optional: if null, use currently logged in user
}

public class UpdateOrderStatusRequest
{
    public int Status { get; set; }
}
