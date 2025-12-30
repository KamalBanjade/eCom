namespace Commerce.Domain.Exceptions;

/// <summary>
/// Exception thrown when attempting to confirm stock without an active reservation
/// </summary>
public class ReservationNotFoundException : Exception
{
    public Guid ProductVariantId { get; }
    public string UserId { get; }
    
    public ReservationNotFoundException(Guid productVariantId, string userId)
        : base($"No active reservation found for variant {productVariantId} and user {userId}")
    {
        ProductVariantId = productVariantId;
        UserId = userId;
    }
}
