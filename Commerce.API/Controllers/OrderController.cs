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
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Creates a new order for the authenticated customer
    /// </summary>
    /// <returns>The created order details</returns>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrder(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var order = await _orderService.CreateOrderAsync(userId.Value, cancellationToken);
        return base.CreatedAtAction(nameof(GetOrder), new { id = order.Id }, ApiResponse<OrderDto>.SuccessResponse(order, "Order created successfully"));
    }

    /// <summary>
    /// Retrieves all orders for the currently authenticated customer
    /// </summary>
    /// <returns>List of orders</returns>
    [HttpGet]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ApiResponse<IEnumerable<OrderDto>>>> GetMyOrders(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

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
    [Authorize] // Any authenticated user can try, but service checks ownership
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("Warehouse");
        
        var order = await _orderService.GetOrderByIdAsync(id, userId.Value, isAdmin, cancellationToken);
        
        if (order == null)
            return NotFound(ApiResponse<OrderDto>.ErrorResponse("Order not found or access denied"));

        return Ok(ApiResponse<OrderDto>.SuccessResponse(order));
    }

    /// <summary>
    /// Admin: Retrieves all orders in the system
    /// </summary>
    /// <returns>List of all orders</returns>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Warehouse")]
    public async Task<ActionResult<ApiResponse<IEnumerable<OrderDto>>>> GetAllOrders(CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetAllOrdersAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<OrderDto>>.SuccessResponse(orders));
    }

    /// <summary>
    /// Admin: Updates the status of an order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="request">New status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated order</returns>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,SuperAdmin,Warehouse")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateStatus(
        Guid id, 
        [FromBody] UpdateOrderStatusRequest request, 
        CancellationToken cancellationToken)
    {
        try 
        {
            var order = await _orderService.UpdateOrderStatusAsync(id, request.Status, cancellationToken);
            return Ok(ApiResponse<OrderDto>.SuccessResponse(order, "Order status updated"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<OrderDto>.ErrorResponse("Order not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<OrderDto>.ErrorResponse($"Failed to update status: {ex.Message}"));
        }
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

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}
