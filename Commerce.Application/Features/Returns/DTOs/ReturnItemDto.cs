using Commerce.Application.Features.Orders.DTOs;

namespace Commerce.Application.Features.Returns.DTOs;

public class ReturnItemDto : OrderItemDto
{
    public Guid ReturnItemId { get; set; }
    public Guid OrderItemId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? AdminNotes { get; set; }
    public bool IsRestocked { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public decimal RefundAmount { get; set; }
}
