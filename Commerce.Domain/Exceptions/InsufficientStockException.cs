namespace Commerce.Domain.Exceptions;

/// <summary>
/// Exception thrown when there is insufficient stock to fulfill a reservation or confirmation
/// </summary>
public class InsufficientStockException : Exception
{
    public Guid ProductVariantId { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }
    
    public InsufficientStockException(Guid productVariantId, int requested, int available)
        : base($"Insufficient stock for variant {productVariantId}. Requested: {requested}, Available: {available}")
    {
        ProductVariantId = productVariantId;
        RequestedQuantity = requested;
        AvailableQuantity = available;
    }
}
