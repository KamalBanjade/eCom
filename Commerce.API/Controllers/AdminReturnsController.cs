using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Returns.DTOs;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[ApiController]
[Route("api/admin/returns")]
[Authorize(Policy = "RequireAdminRole")]
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
    public async Task<ActionResult<ApiResponse<ReturnRequest>>> AssignReturn(
        Guid id,
        [FromBody] AssignReturnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _returnService.AssignReturnAsync(id, request.AssignedToUserId, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
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
    public async Task<ActionResult<ApiResponse<ReturnRequest>>> ApproveReturn(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.ApproveReturnAsync(id, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPatch("{id}/reject")]
    public async Task<ActionResult<ApiResponse<ReturnRequest>>> RejectReturn(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.RejectReturnAsync(id, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPatch("{id}/receive")]
    public async Task<ActionResult<ApiResponse<ReturnRequest>>> MarkReceived(Guid id, CancellationToken cancellationToken)
    {
        var result = await _returnService.MarkReceivedAsync(id, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPatch("{id}/refund")]
    public async Task<ActionResult<ApiResponse<ReturnRequest>>> ProcessRefund(
        Guid id, 
        [FromBody] ProcessRefundRequestDto request, 
        CancellationToken cancellationToken)
    {
        var result = await _returnService.ProcessRefundAsync(id, request.RefundMethod, request.RefundAmount, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}

public class ProcessRefundRequestDto
{
    public RefundMethod RefundMethod { get; set; }
    public decimal RefundAmount { get; set; }
}