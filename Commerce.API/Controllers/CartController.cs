using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<Commerce.Domain.Entities.Carts.Cart>>> GetCart([FromQuery] string? anonymousId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null && string.IsNullOrEmpty(anonymousId))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Either Authenticated User or AnonymousId required"));
        }

        var result = await _cartService.GetCartAsync(userId, anonymousId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("items")]
    public async Task<ActionResult<ApiResponse<Commerce.Domain.Entities.Carts.Cart>>> AddItem(
        [FromBody] AddCartItemRequest request, 
        [FromQuery] string? anonymousId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _cartService.AddItemAsync(userId, anonymousId, request.ProductVariantId, request.Quantity, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("items/{itemId}")]
    public async Task<ActionResult<ApiResponse<Commerce.Domain.Entities.Carts.Cart>>> RemoveItem(
        Guid itemId, 
        [FromQuery] string? anonymousId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _cartService.RemoveItemAsync(userId, anonymousId, itemId, cancellationToken);
         if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("transfer")]
    [Authorize] // Must be logged in to transfer
    public async Task<ActionResult<ApiResponse<bool>>> TransferCart(
        [FromQuery] string anonymousId, 
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(); // Should be handled by [Authorize]

        var result = await _cartService.TransferAnonymousCartToCustomerAsync(anonymousId, userId.Value, cancellationToken);
        return Ok(result);
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

public class AddCartItemRequest
{
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }
}
