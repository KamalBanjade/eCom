using Commerce.Application.Features.Orders;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/payments")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IOrderService orderService, ILogger<PaymentsController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Khalti payment callback endpoint
    /// Called by Khalti after user completes payment
    /// </summary>
    [HttpGet("khalti/callback")]
    public async Task<IActionResult> KhaltiCallback(
        [FromQuery] string pidx,
        [FromQuery] string? txnId,
        [FromQuery] string? amount,
        [FromQuery] string? mobile,
        [FromQuery] string? purchase_order_id,
        [FromQuery] string? purchase_order_name,
        [FromQuery] string? transaction_id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Khalti callback received. Pidx: {Pidx}, TxnId: {TxnId}", pidx, txnId);

        if (string.IsNullOrEmpty(pidx))
        {
            _logger.LogWarning("Khalti callback received without pidx");
            return BadRequest("Invalid callback: missing pidx");
        }

        // Verify payment via Lookup API (NEVER trust query params alone)
        var result = await _orderService.ConfirmPaymentAsync(pidx, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Payment confirmed successfully for Pidx: {Pidx}", pidx);
            
            // Redirect to success page or return JSON for SPA
            // For now, return JSON response
            return Ok(new
            {
                success = true,
                message = "Payment confirmed successfully",
                orderId = result.Data?.Id,
                orderNumber = result.Data?.OrderNumber
            });
        }
        else
        {
            _logger.LogWarning("Payment confirmation failed for Pidx: {Pidx}. Reason: {Reason}", pidx, result.Message);
            
            return BadRequest(new
            {
                success = false,
                message = result.Message
            });
        }
    }
}
