using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Payments;
using Commerce.Application.Features.Payments.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/admin/payments")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminPaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public AdminPaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// Admin: List all payments with filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentDto>>>> GetPayments(
        [FromQuery] PaymentFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.GetAllPaymentsAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<PaymentDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Get payment details for specific order
    /// </summary>
    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPaymentByOrderId(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.GetPaymentByOrderIdAsync(orderId, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Payment/Order not found"));
            
        return Ok(ApiResponse<PaymentDto>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Manually verify a payment
    /// </summary>
    [HttpPost("order/{orderId}/verify")]
    public async Task<ActionResult<ApiResponse<bool>>> VerifyPayment(
        Guid orderId,
        [FromBody] VerifyPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.ManuallyVerifyPaymentAsync(orderId, request, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
