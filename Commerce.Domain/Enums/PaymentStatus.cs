namespace Commerce.Domain.Enums;

public enum PaymentStatus
{
    NotRequired = 0,  // COD orders
    Pending = 1,
    Initiated = 2,    // Khalti: payment initiated
    Authorized = 3,
    Completed = 4,    // Khalti: payment successful (was Captured)
    Failed = 5,
    Refunded = 6
}
