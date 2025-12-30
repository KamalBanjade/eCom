using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Payments.DTOs;

namespace Commerce.Application.Features.Payments;

/// <summary>
/// Service for admin payment operations
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Gets paginated list of all payments with filtering
    /// </summary>
    /// <param name="filter">Filter parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of payments</returns>
    Task<PagedResult<PaymentDto>> GetAllPaymentsAsync(
        PaymentFilterRequest filter, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets payment details for a specific order
    /// </summary>
    /// <param name="orderId">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment details or null if not found</returns>
    Task<PaymentDto?> GetPaymentByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Manually triggers Khalti payment verification
    /// </summary>
    /// <param name="pidx">Khalti payment identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification result</returns>
    Task<ApiResponse<bool>> ManuallyVerifyPaymentAsync(Guid orderId, VerifyPaymentRequest request, CancellationToken cancellationToken = default);
}
