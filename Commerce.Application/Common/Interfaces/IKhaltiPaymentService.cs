// Update your IKhaltiPaymentService.cs interface

using Commerce.Application.Features.Payments.DTOs;

namespace Commerce.Application.Common.Interfaces;

public interface IKhaltiPaymentService
{
    Task<KhaltiInitiateResponse> InitiatePaymentAsync(
        KhaltiInitiateRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<KhaltiLookupResponse> LookupPaymentAsync(
        string pidx, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refund a Khalti transaction
    /// </summary>
    /// <param name="transactionId">Transaction ID from lookup response (NOT pidx)</param>
    /// <param name="amountInPaisa">Amount to refund in paisa. Null for full refund.</param>
    /// <param name="mobile">Mobile number for bank refunds. Required for bank transactions.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refund response from Khalti</returns>
    Task<KhaltiRefundResponse> RefundPaymentAsync(
        string transactionId,
        long? amountInPaisa = null,
        string? mobile = null,
        CancellationToken cancellationToken = default);
}