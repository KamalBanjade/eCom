namespace Commerce.Domain.Enums;

public enum OrderStatus
{
    PendingPayment = 1,  // Khalti: waiting for payment
    Confirmed = 2,       // COD: auto-confirmed | Khalti: payment verified
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Returned = 7
}
