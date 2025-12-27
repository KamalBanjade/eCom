using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Places a new order for the authenticated customer
    /// </summary>
    /// <param name="request">Order placement request with payment method and shipping address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created order details</returns>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) 
            return Unauthorized(ApiResponse<OrderDto>.ErrorResponse("User not authenticated"));

        var result = await _orderService.PlaceOrderAsync(userId.Value, request, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(
            nameof(GetOrder), 
            new { id = result.Data!.Id }, 
            result);
    }

    /// <summary>
    /// Retrieves all orders for the currently authenticated customer
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of orders</returns>
    [HttpGet]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ApiResponse<IEnumerable<OrderDto>>>> GetMyOrders(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) 
            return Unauthorized(ApiResponse<IEnumerable<OrderDto>>.ErrorResponse("User not authenticated"));

        var orders = await _orderService.GetUserOrdersAsync(userId.Value, cancellationToken);
        return Ok(ApiResponse<IEnumerable<OrderDto>>.SuccessResponse(orders));
    }

    /// <summary>
    /// Retrieves a specific order by ID. Customers can only see their own orders.
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details</returns>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(
        Guid id, 
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) 
            return Unauthorized(ApiResponse<OrderDto>.ErrorResponse("User not authenticated"));

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        
        var order = await _orderService.GetOrderByIdAsync(id, userId.Value, isAdmin, cancellationToken);
        
        if (order == null)
            return NotFound(ApiResponse<OrderDto>.ErrorResponse("Order not found or access denied"));

        return Ok(ApiResponse<OrderDto>.SuccessResponse(order));
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}
