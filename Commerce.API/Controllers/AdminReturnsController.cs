using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth;
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

    public AdminReturnsController(IReturnService returnService)
    {
        _returnService = returnService;
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
