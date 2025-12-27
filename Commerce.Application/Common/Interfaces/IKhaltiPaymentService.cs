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
}
