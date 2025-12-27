namespace Commerce.Application.Common.DTOs;

public class CartResponse
{
    public string CartId { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }                 
    public decimal DiscountAmount { get; set; }        
    public string? AppliedCoupon { get; set; }
    public int TotalItems { get; set; }
    public List<CartItemResponse> Items { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

public class CartItemResponse
{
    public Guid ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}