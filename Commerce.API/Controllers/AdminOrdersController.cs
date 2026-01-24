using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/admin/orders")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Warehouse,Support")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IExportService _exportService;

    public AdminOrdersController(IOrderService orderService, IExportService exportService)
    {
        _orderService = orderService;
        _exportService = exportService;
    }

    /// <summary>
    /// Admin: Retrieves all orders with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetOrders(
        [FromQuery] OrderFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.GetOrdersAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<OrderDto>>.SuccessResponse(result));
    }
    
    /// <summary>
    /// Admin: Quick view for orders pending payment (Khalti unpaid)
    /// </summary>
    [HttpGet("pending-payment")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetPendingPaymentOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderService.GetPendingPaymentOrdersAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<OrderDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Quick view for orders with return requests
    /// </summary>
    [HttpGet("returns")]
    [Authorize(Roles = "Admin,SuperAdmin,Support,Warehouse")]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetOrdersWithReturns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderService.GetOrdersWithReturnsAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<OrderDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Retrieves a specific order by ID with full details
    /// </summary>
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
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,SuperAdmin,Warehouse")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        // RBAC Logic for Warehouse
        if (User.IsInRole("Warehouse"))
        {
            if (request.Status != OrderStatus.Processing && request.Status != OrderStatus.Shipped)
            {
                return StatusCode(403, ApiResponse<OrderDto>.ErrorResponse("Warehouse staff can only update status to Processing or Shipped"));
            }
        }

        var result = await _orderService.UpdateOrderStatusAsync(id, request.Status, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
    
    /// <summary>
    /// Admin: Assigns an order to warehouse or support staff
    /// </summary>
    [HttpPatch("{id}/assign")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> AssignOrder(
        Guid id,
        [FromBody] AssignOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.AssignOrderAsync(id, request.AssignedToUserId, request.AssignedRole, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);
            
        return Ok(result);
    }
    
    /// <summary>
    /// Admin: Exports orders to CSV based on filters
    /// </summary>
    [HttpPost("export")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ExportOrders(
        [FromBody] OrderFilterRequest filter,
        CancellationToken cancellationToken)
    {
        // Get all matching orders (ignoring pagination for export)
        filter.Page = 1;
        filter.PageSize = 10000; 
        
        var result = await _orderService.GetOrdersAsync(filter, cancellationToken);
        var csvBytes = await _exportService.ExportOrdersToCsvAsync(result.Items, cancellationToken);
        
        return File(csvBytes, "text/csv", $"orders_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}