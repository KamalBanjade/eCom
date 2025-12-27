namespace Commerce.Domain.Enums;

public enum PaymentStatus
{
    NotRequired = 1,  // COD orders
    Initiated = 2,    // Khalti: payment initiated
    Completed = 3,    // Khalti: payment successful
    Failed = 4,
    Refunded = 5
}
