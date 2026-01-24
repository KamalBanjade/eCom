using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth;
using Commerce.Domain.Entities.Sales;
using Commerce.Application.Features.Returns.DTOs;
using Commerce.Application.Features.Orders.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Commerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Customer)]
public class ReturnsController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnsController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    [HttpPost]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReturnRequestDto>>> RequestReturn(
        [FromBody] CreateReturnRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _returnService.RequestReturnAsync(request.OrderId, request.Items, userId, cancellationToken);
        
        if (!result.Success) 
        {
             return BadRequest(new ApiResponse<ReturnRequestDto> 
             { 
                 Success = false, 
                 Message = result.Message 
             });
        }

        var returnRequest = result.Data!;
        // Re-use Logic from Service if possible, otherwise manual map
        var dto = new ReturnRequestDto
        {
            Id = returnRequest.Id,
            OrderId = returnRequest.OrderId,
            OrderNumber = returnRequest.Order?.OrderNumber ?? "",
            Reason = string.Join(", ", returnRequest.Items?.Select(i => i.Reason) ?? Enumerable.Empty<string>()),
            ReturnStatus = returnRequest.ReturnStatus.ToString(),
            RefundAmount = returnRequest.TotalRefundAmount,
            RefundMethod = returnRequest.RefundMethod?.ToString(),
            RequestedAt = returnRequest.RequestedAt,
            ApprovedAt = returnRequest.ApprovedAt,
            ReceivedAt = returnRequest.ReceivedAt,
            RefundedAt = returnRequest.RefundedAt,
            AssignedToUserId = returnRequest.AssignedToUserId,
            AssignedRole = returnRequest.AssignedRole,
            AssignedAt = returnRequest.AssignedAt,
            CustomerName = returnRequest.Order?.CustomerProfile?.FullName ?? "",
            CustomerEmail = returnRequest.Order?.CustomerProfile?.Email ?? "",
            Items = returnRequest.Items?.Select(ri => 
            {
               var orderItem = returnRequest.Order?.Items.FirstOrDefault(oi => oi.Id == ri.OrderItemId);
               return new ReturnItemDto
               {
                   ReturnItemId = ri.Id,
                   OrderItemId = ri.OrderItemId,
                   Reason = ri.Reason,
                   Status = ri.Status.ToString(),
                   Quantity = ri.Quantity,
                   UnitPrice = ri.UnitPrice,
                   ProductName = orderItem?.ProductName ?? "Unknown",
                   VariantName = orderItem?.VariantName,
                   ProductVariantId = orderItem?.ProductVariantId ?? Guid.Empty,
                   Id = orderItem?.Id ?? Guid.Empty, // Compat
                   RefundAmount = ri.Quantity * ri.UnitPrice
               };
            }).ToList() ?? new()
        };
        
        return Ok(ApiResponse<ReturnRequestDto>.SuccessResponse(dto, result.Message)); 
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReturnRequest>>> GetMyReturns(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var returns = await _returnService.GetUserReturnsAsync(userId, cancellationToken);
        return Ok(ApiResponse<IEnumerable<ReturnRequest>>.SuccessResponse(returns, "Returns retrieved successfully"));
    }
}

public class CreateReturnRequestDto
{
    public Guid OrderId { get; set; }
    public List<CreateReturnItemDto> Items { get; set; } = new();
}
