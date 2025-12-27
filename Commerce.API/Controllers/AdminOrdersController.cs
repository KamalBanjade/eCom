using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/admin/orders")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public AdminOrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Admin: Retrieves all orders with pagination and filtering
    /// </summary>
    /// <param name="filter">Filter parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of orders</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetOrders(
        [FromQuery] OrderFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.GetOrdersAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<OrderDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Retrieves a specific order by ID with full details
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Admin can view any order, pass a dummy userId and isAdmin=true
        var order = await _orderService.GetOrderByIdAsync(id, Guid.Empty, true, cancellationToken);
        
        if (order == null)
            return NotFound(ApiResponse<OrderDto>.ErrorResponse("Order not found"));

        return Ok(ApiResponse<OrderDto>.SuccessResponse(order));
    }

    /// <summary>
    /// Admin: Updates the status of an order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="request">New status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated order</returns>
    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.UpdateOrderStatusAsync(id, request.Status, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
