using Commerce.API.DTOs;
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
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartResponse>>> GetCart(
        [FromQuery] string? anonymousId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null && string.IsNullOrEmpty(anonymousId))
        {
            return BadRequest(ApiResponse<CartResponse>.ErrorResponse("Either authenticated user or anonymousId is required"));
        }

        var result = await _cartService.GetCartAsync(userId, anonymousId, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("items")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartResponse>>> AddItem(
        [FromBody] AddCartItemRequest request,
        [FromQuery] string? anonymousId,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
            return BadRequest(ApiResponse<CartResponse>.ErrorResponse("Quantity must be greater than zero"));

        // Debug: Check if we have an authorization header but no user
        if (User.Identity?.IsAuthenticated != true && Request.Headers.ContainsKey("Authorization"))
        {
            // Token was sent but validation failed (or middleware refused it), yet we are here because of AllowAnonymous.
            // Retrieve 401 is better than "User required" logic below.
            return Unauthorized(ApiResponse<CartResponse>.ErrorResponse("Invalid or expired token"));
        }

        var userId = GetUserId();
        if (userId == null && string.IsNullOrEmpty(anonymousId))
        {
            return BadRequest(ApiResponse<CartResponse>.ErrorResponse("Either authenticated user or anonymousId is required"));
        }

        var result = await _cartService.AddItemAsync(
            userId,
            anonymousId,
            request.ProductVariantId,
            request.Quantity,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpDelete("items/{itemId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartResponse>>> RemoveItem(
        Guid itemId,
        [FromQuery] string? anonymousId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null && string.IsNullOrEmpty(anonymousId))
        {
            return BadRequest(ApiResponse<CartResponse>.ErrorResponse("Either authenticated user or anonymousId is required"));
        }

        var result = await _cartService.RemoveItemAsync(userId, anonymousId, itemId, cancellationToken);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpDelete]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> ClearCart(
        [FromQuery] string? anonymousId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null && string.IsNullOrEmpty(anonymousId))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse("Either authenticated user or anonymousId is required"));
        }

        var result = await _cartService.ClearCartAsync(userId, anonymousId, cancellationToken);

        return Ok(result);
    }

    [HttpPost("transfer")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> TransferCart(
        [FromQuery] string anonymousId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        // [Authorize] already ensures user is logged in, but double-check
        if (userId == null)
            return Unauthorized();

        var result = await _cartService.TransferAnonymousCartToCustomerAsync(
            anonymousId,
            userId.Value,
            cancellationToken);

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        // Debug info could be logged here
        
        // 1. Try standard NameIdentifier
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        // 2. Try "sub" (Subject) - Standard JWT claim
        var subClaim = User.FindFirst("sub");
        if (subClaim != null && Guid.TryParse(subClaim.Value, out var subId))
        {
            return subId;
        }

        // 3. Try "id" - Custom or alternate mapping
        var idClaim = User.FindFirst("id");
        if (idClaim != null && Guid.TryParse(idClaim.Value, out var id))
        {
            return id;
        }

        return null;
    }
}

// Request DTO - placed here for convenience (or move to separate DTOs folder)
public class AddCartItemRequest
{
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }
}