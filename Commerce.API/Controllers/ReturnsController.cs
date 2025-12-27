using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Auth;
using Commerce.Domain.Entities.Sales;
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
    public async Task<ActionResult<ApiResponse<ReturnRequest>>> RequestReturn(
        [FromBody] CreateReturnRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _returnService.RequestReturnAsync(request.OrderId, request.Reason, userId, cancellationToken);
        
        if (!result.Success) return BadRequest(result);
        return Ok(result);
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
    public string Reason { get; set; } = string.Empty;
}
