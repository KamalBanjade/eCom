using Commerce.API.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _couponService;
    private readonly Application.Features.Carts.ICartService _cartService;

    public CouponsController(ICouponService couponService, Application.Features.Carts.ICartService cartService)
    {
        _couponService = couponService;
        _cartService = cartService;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> CreateCoupon(
        [FromBody] CreateCouponRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Invalid request data" });

        try
        {
            var coupon = new Coupon
            {
                Code = request.Code,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                ExpiryDate = request.ExpiryDate,
                MaxUses = request.MaxUses,
                MinOrderAmount = request.MinOrderAmount,
                CurrentUses = 0,
                IsActive = true
            };

            var created = await _couponService.CreateCouponAsync(coupon, cancellationToken);

            return CreatedAtAction(
                nameof(GetCoupon),
                new { code = created.Code },
                new { success = true, data = created, message = "Coupon created successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "An error occurred while creating the coupon: " + ex.Message });
        }
    }

    [HttpGet("{code}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetCoupon(string code, CancellationToken cancellationToken)
    {
        var coupon = await _couponService.GetCouponByCodeAsync(code, cancellationToken);
        if (coupon == null)
            return NotFound(new { success = false, message = "Coupon not found" });

        return Ok(new { success = true, data = coupon });
    }

    [HttpDelete("{code}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeactivateCoupon(string code, CancellationToken cancellationToken)
    {
        var result = await _couponService.DeactivateCouponAsync(code, cancellationToken);
        if (!result)
            return NotFound(new { success = false, message = "Coupon not found" });

        return Ok(new { success = true, data = true, message = "Coupon deactivated successfully" });
    }

    [HttpPost("apply")]
    [Authorize]
    public async Task<IActionResult> ApplyCoupon(
        [FromBody] ApplyCouponRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Invalid request" });

        // Get authenticated user ID from JWT token
        var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid applicationUserId))
            return Unauthorized(new { success = false, message = "Invalid or missing user authentication" });

        // For authenticated users, pass the applicationUserId
        // The CartService will handle converting it to CustomerProfileId
        var response = await _cartService.ApplyCouponAsync(applicationUserId, null, request.Code, cancellationToken);
        
        if (!response.Success)
            return BadRequest(new { success = false, message = response.Message });

        return Ok(new { success = true, data = response.Data, message = "Coupon applied successfully" });
    }
}