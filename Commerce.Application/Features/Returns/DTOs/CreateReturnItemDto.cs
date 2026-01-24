namespace Commerce.Application.Features.Returns.DTOs;

public class CreateReturnItemDto
{
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Condition { get; set; }
}
