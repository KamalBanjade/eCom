using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Returns.DTOs;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Enums;
using Commerce.Application.Features.Orders.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[ApiController]
[Route("api/admin/returns")]
[Authorize(Roles = "Admin,SuperAdmin,Support,Warehouse")]
public class AdminReturnsController : ControllerBase
{
    private readonly IReturnService _returnService;
    private readonly IExportService _exportService;

    public AdminReturnsController(IReturnService returnService, IExportService exportService)
    {
        _returnService = returnService;
        _exportService = exportService;
    }
    
    /// <summary>
    /// Admin: List all return requests with filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ReturnRequestDto>>>> GetAllReturns(
        [FromQuery] ReturnFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _returnService.GetAllReturnsAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<ReturnRequestDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Admin: Get specific return request details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> GetReturnById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _returnService.GetReturnByIdAsync(id, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<ReturnRequestDto>.ErrorResponse("Return request not found"));
            
        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(result));
    }
    
    /// <summary>
    /// Admin: Assign return request to support/warehouse
    /// </summary>
    [HttpPatch("{id}/assign")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> AssignReturn(
        Guid id,
        [FromBody] AssignReturnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _returnService.AssignReturnAsync(id, request.AssignedToUserId, cancellationToken);
        if (!result.Success) 
            return BadRequest(ApiResponse<ReturnRequestDto>.ErrorResponse(result.Message));
        
        // Map to DTO
        var dto = MapToDto(result.Data);
        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(dto, result.Message));
    }
    
    /// <summary>
    /// Admin: Export return requests to CSV
    /// </summary>
    [HttpPost("export")]
    public async Task<IActionResult> ExportReturns(
        [FromBody] ReturnFilterRequest filter,
        CancellationToken cancellationToken)
    {
        filter.Page = 1;
        filter.PageSize = 10000;
        
        var result = await _returnService.GetAllReturnsAsync(filter, cancellationToken);
        // ✅ FIXED: Changed from result.Data to result.Items
        var csvBytes = await _exportService.ExportReturnsToCsvAsync(result.Items, cancellationToken);
        
        return File(csvBytes, "text/csv", $"returns_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPatch("{id}/approve")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> ApproveReturn(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.ApproveReturnAsync(id, cancellationToken);
        if (!result.Success) 
             return BadRequest(ApiResponse<ReturnRequestDto>.ErrorResponse(result.Message));

        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(MapToDto(result.Data), result.Message));
    }

    [HttpPatch("{id}/reject")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> RejectReturn(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.RejectReturnAsync(id, cancellationToken);
        if (!result.Success) 
             return BadRequest(ApiResponse<ReturnRequestDto>.ErrorResponse(result.Message));

        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(MapToDto(result.Data), result.Message));
    }

    [HttpPatch("{id}/receive")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> MarkReceived(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.MarkReceivedAsync(id, cancellationToken);
        if (!result.Success) 
             return BadRequest(ApiResponse<ReturnRequestDto>.ErrorResponse(result.Message));

        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(MapToDto(result.Data), result.Message));
    }

    [HttpPatch("{id}/pickup")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> MarkPickedUp(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.MarkPickedUpAsync(id, cancellationToken);
        if (!result.Success) 
             return BadRequest(ApiResponse<ReturnRequestDto>.ErrorResponse(result.Message));

        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(MapToDto(result.Data), result.Message));
    }

    [HttpPatch("{id}/inspect")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> CompleteInspection(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.CompleteInspectionAsync(id, cancellationToken);
        if (!result.Success) 
             return BadRequest(ApiResponse<ReturnRequestDto>.ErrorResponse(result.Message));

        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(MapToDto(result.Data), result.Message));
    }

    [HttpPatch("{id}/refund")]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> ProcessRefund(
        Guid id, 
        [FromBody] ProcessRefundRequestDto request, 
        CancellationToken cancellationToken)
    {
        // DIAGNOSTIC LOG: Incoming refund request
        Console.WriteLine("==========================================================");
        Console.WriteLine($"[REFUND REQUEST RECEIVED] Return ID: {id}");
        Console.WriteLine($"[REFUND REQUEST] Method: {request.RefundMethod}");
        Console.WriteLine($"[REFUND REQUEST] Amount: {request.RefundAmount}");
        Console.WriteLine("==========================================================");
        
        var result = await _returnService.ProcessRefundAsync(id, request.RefundMethod, request.RefundAmount, cancellationToken);
        
        // DIAGNOSTIC LOG: Service response
        Console.WriteLine("==========================================================");
        Console.WriteLine($"[REFUND RESPONSE] Success: {result.Success}");
        Console.WriteLine($"[REFUND RESPONSE] Message: {result.Message}");
        Console.WriteLine("==========================================================");
        
        if (!result.Success) 
             return BadRequest(ApiResponse<ReturnRequestDto>.ErrorResponse(result.Message));

        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(MapToDto(result.Data), result.Message));
    }

    private static ReturnRequestDto MapToDto(ReturnRequest request)
    {
        if (request == null) return null;
        
        return new ReturnRequestDto
        {
            Id = request.Id,
            OrderId = request.OrderId,
            OrderNumber = request.Order?.OrderNumber ?? "N/A",
            Reason = string.Join(", ", request.Items?.Select(i => i.Reason) ?? Enumerable.Empty<string>()),
            ReturnStatus = request.ReturnStatus.ToString(),
            RefundAmount = request.TotalRefundAmount,
            RefundMethod = request.RefundMethod?.ToString(),
            KhaltiPidx = request.KhaltiPidx,
            
            AssignedToUserId = request.AssignedToUserId,
            AssignedAt = request.AssignedAt,
            
            RequestedAt = request.RequestedAt,
            ApprovedAt = request.ApprovedAt,
            PickedUpAt = request.PickedUpAt,
            ReceivedAt = request.ReceivedAt,
            InspectionCompletedAt = request.InspectionCompletedAt,
            RefundedAt = request.RefundedAt,
            
            CustomerEmail = request.Order?.CustomerProfile?.Email ?? "",
            CustomerName = request.Order?.CustomerProfile?.FullName ?? "",
            
            Items = request.Items?.Select(ri => 
            {
               var orderItem = request.Order?.Items.FirstOrDefault(oi => oi.Id == ri.OrderItemId);
               return new ReturnItemDto
               {
                   ReturnItemId = ri.Id,
                   OrderItemId = ri.OrderItemId,
                   Reason = ri.Reason,
                   Status = ri.Status.ToString(),
                   Condition = ri.Condition,
                   AdminNotes = ri.AdminNotes,
                   IsRestocked = ri.IsRestocked,
                   ReceivedAt = ri.ReceivedAt,
                   Quantity = ri.Quantity,
                   UnitPrice = ri.UnitPrice,
                   ProductName = orderItem?.ProductName ?? "Unknown",
                   VariantName = orderItem?.VariantName,
                   ProductVariantId = orderItem?.ProductVariantId ?? Guid.Empty,
                   Id = orderItem?.Id ?? Guid.Empty,
                   RefundAmount = ri.Quantity * ri.UnitPrice
               };
            }).ToList() ?? new List<ReturnItemDto>()
        };
    }
}

public class ProcessRefundRequestDto
{
    public RefundMethod RefundMethod { get; set; }
    public decimal RefundAmount { get; set; }
}