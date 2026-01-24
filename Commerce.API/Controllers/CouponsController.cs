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
                ExpiryDate = DateTime.SpecifyKind(request.ExpiryDate, DateTimeKind.Utc),
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

    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAllCoupons(CancellationToken cancellationToken)
    {
        var coupons = await _couponService.GetAllCouponsAsync(cancellationToken);
        return Ok(new { success = true, data = coupons });
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

    [HttpPut("{code}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateCoupon(string code, [FromBody] UpdateCouponRequest request, CancellationToken cancellationToken)
    {
        if (code != request.Code) // Optional check if code matches
             return BadRequest("Code mismatch");

        var coupon = await _couponService.GetCouponByCodeAsync(code, cancellationToken);
        if (coupon == null)
            return NotFound(new { success = false, message = "Coupon not found" });

        // Map updates
        coupon.DiscountType = request.DiscountType;
        coupon.DiscountValue = request.DiscountValue;
        coupon.ExpiryDate = DateTime.SpecifyKind(request.ExpiryDate, DateTimeKind.Utc);
        coupon.MaxUses = request.MaxUses;
        coupon.MinOrderAmount = request.MinOrderAmount;
        coupon.IsActive = request.IsActive;

        await _couponService.UpdateCouponAsync(coupon, cancellationToken);

        return Ok(new { success = true, data = coupon, message = "Coupon updated successfully" });
    }

    [HttpDelete("{code}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteCoupon(string code, CancellationToken cancellationToken)
    {
        try 
        {
            var result = await _couponService.DeleteCouponAsync(code, cancellationToken);
            if (!result)
                return NotFound(new { success = false, message = "Coupon not found" });

            return Ok(new { success = true, data = true, message = "Coupon deleted successfully" });
        }
        catch (Exception ex)
        {
            // Handle FK constraints
            return Conflict(new { success = false, message = "Cannot delete coupon. It might be in use." });
        }
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