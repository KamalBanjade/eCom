using Commerce.Domain.Enums;

namespace Commerce.Application.Features.Payments.DTOs;

/// <summary>
/// DTO for payment information
/// </summary>
public class PaymentDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    
    // Khalti specific
    public string? TransactionId { get; set; }
    public string? PaymentUrl { get; set; }
    public DateTime? PaidAt { get; set; }
    
    // Customer info
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public DateTime OrderCreatedAt { get; set; }
}

/// <summary>
/// Filter request for payments
/// </summary>
public class PaymentFilterRequest
{
    public PaymentMethod? PaymentMethod { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; } // Search by order number or customer email
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Request to manually verify a payment
/// </summary>
public class VerifyPaymentRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}
