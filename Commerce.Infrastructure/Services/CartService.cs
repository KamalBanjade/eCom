using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Carts;
using Commerce.Application.Features.Inventory;
using Commerce.Domain.Entities.Carts;
using Commerce.Domain.Entities.Products;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Commerce.Domain.Entities.Sales;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

using System.Text.Json.Serialization;

namespace Commerce.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CommerceDbContext _context;
    private readonly ICouponService _couponService;
    private readonly IInventoryService _inventoryService;
    private readonly IDatabase _db;
    private const int CartTtlSeconds = 86400 * 30; // 30 days
    private static readonly TimeSpan ReservationDuration = TimeSpan.FromMinutes(15);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private decimal ApplyCouponDiscount(decimal amount, Coupon coupon)
{
    if (coupon.DiscountType == DiscountType.Percentage)
    {
        return amount - amount * (coupon.DiscountValue / 100m);
    }
    return amount - coupon.DiscountValue;
}
    public CartService(
        IConnectionMultiplexer redis,
        UserManager<ApplicationUser> userManager,
        CommerceDbContext context,
        ICouponService couponService,
        IInventoryService inventoryService)
    {
        _redis = redis;
        _userManager = userManager;
        _context = context;
        _couponService = couponService;
        _inventoryService = inventoryService;
        _db = _redis.GetDatabase();
    }

    private string GetCartKey(Guid? customerId, string? anonymousId)
    {
        if (customerId.HasValue)
            return $"cart:customer:{customerId.Value}";
        if (!string.IsNullOrEmpty(anonymousId))
            return $"cart:anonymous:{anonymousId}";

        throw new ArgumentException("Either CustomerId or AnonymousId must be provided");
    }

    private async Task<Guid?> GetCustomerProfileIdAsync(Guid? applicationUserId)
    {
        if (!applicationUserId.HasValue) return null;

        var user = await _userManager.FindByIdAsync(applicationUserId.Value.ToString());
        return user?.CustomerProfileId;
    }

    private Cart CreateNewCart(Guid? customerId, string? anonymousId)
    {
        if (customerId.HasValue)
            return Cart.CreateForCustomer(customerId.Value);
        return Cart.CreateAnonymous(anonymousId!);
    }

    private async Task<CartResponse> MapToResponseAsync(Cart cart, string key, CancellationToken cancellationToken)
    {
        decimal subtotal = 0;
        int totalItems = 0;
        var responseItems = new List<CartItemResponse>();

        if (cart.Items.Any())
        {
            var variantIds = cart.Items.Select(i => i.ProductVariantId).Distinct().ToList();

            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v, cancellationToken);

            foreach (var item in cart.Items)
            {
                var variant = variants.GetValueOrDefault(item.ProductVariantId);
                decimal price = variant?.DiscountPrice ?? variant?.Price ?? item.PriceAtAdd;

                subtotal += price * item.Quantity;
                totalItems += item.Quantity;

                responseItems.Add(new CartItemResponse
                {
                    ProductVariantId = item.ProductVariantId,
                    ProductName = variant?.Product?.Name ?? "Unknown Product",
                    VariantName = variant?.Attributes != null && variant.Attributes.Any()
                        ? string.Join(" - ", variant.Attributes.Values.Where(v => !string.IsNullOrEmpty(v)))
                        : variant?.SKU ?? string.Empty,
                    ImageUrl = variant?.ImageUrls?.FirstOrDefault() ?? string.Empty,
                    UnitPrice = price,
                    Quantity = item.Quantity,
                });
            }
        }

        // === COUPON CALCULATION ===
        // === COUPON CALCULATION ===
        var appliedCoupon = cart.AppliedCouponCode;
        decimal discountAmount = 0m;
        decimal total = subtotal;

        if (!string.IsNullOrEmpty(appliedCoupon))
        {
            var coupon = await _couponService.ValidateCouponAsync(appliedCoupon, cancellationToken);
            if (coupon != null)
            {
                total = ApplyCouponDiscount(subtotal, coupon);
                total = Math.Max(0, total);
                discountAmount = subtotal - total;
            }
            else
            {
                // Invalid or expired coupon — clean it up
                cart.AppliedCouponCode = null;
                await _db.StringSetAsync(key, JsonSerializer.Serialize(cart, _jsonOptions), TimeSpan.FromSeconds(CartTtlSeconds));
                appliedCoupon = null;
            }
        }

        return new CartResponse
        {
            CartId = key,
            Subtotal = subtotal,
            Total = total,
            DiscountAmount = discountAmount,
            AppliedCoupon = appliedCoupon,
            TotalItems = totalItems,
            Items = responseItems,
            ExpiresAt = DateTime.UtcNow.AddSeconds(CartTtlSeconds)
        };
    }

    public async Task<ApiResponse<CartResponse>> GetCartAsync(
        Guid? applicationUserId,
        string? anonymousId,
        CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);
        if (customerId == null && string.IsNullOrEmpty(anonymousId))
            return ApiResponse<CartResponse>.ErrorResponse("No cart identifier provided");
            
        var key = GetCartKey(customerId, anonymousId);

        try
        {
            var data = await _db.StringGetAsync(key);

            Cart cart;
            if (data.IsNullOrEmpty)
            {
                cart = CreateNewCart(customerId, anonymousId);
            }
            else
            {
                try
                {
                    cart = JsonSerializer.Deserialize<Cart>(data!, _jsonOptions)
                           ?? CreateNewCart(customerId, anonymousId);
                }
                catch (JsonException)
                {
                    await _db.KeyDeleteAsync(key);
                    cart = CreateNewCart(customerId, anonymousId);
                }
            }

            var response = await MapToResponseAsync(cart, key, cancellationToken);

            // Extend TTL on read
            await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(CartTtlSeconds));

            // Save updated cart if needed (e.g., new cart)
            if (data.IsNullOrEmpty || cart.Items.Any())
            {
                await _db.StringSetAsync(key, JsonSerializer.Serialize(cart, _jsonOptions), TimeSpan.FromSeconds(CartTtlSeconds));
            }

            return ApiResponse<CartResponse>.SuccessResponse(response);
        }
        catch (RedisConnectionException)
        {
            return ApiResponse<CartResponse>.ErrorResponse("Cart service temporarily unavailable");
        }
    }

    public async Task<ApiResponse<CartResponse>> AddItemAsync(
        Guid? applicationUserId,
        string? anonymousId,
        Guid productVariantId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) return ApiResponse<CartResponse>.ErrorResponse("Quantity must be positive");

        var customerId = await GetCustomerProfileIdAsync(applicationUserId);
        if (customerId == null && string.IsNullOrEmpty(anonymousId))
            return ApiResponse<CartResponse>.ErrorResponse("No cart identifier provided");

        var key = GetCartKey(customerId, anonymousId);

        try
        {
            // Get or create cart
            var data = await _db.StringGetAsync(key);
            Cart cart = data.IsNullOrEmpty
                ? CreateNewCart(customerId, anonymousId)
                : JsonSerializer.Deserialize<Cart>(data!, _jsonOptions) ?? CreateNewCart(customerId, anonymousId);

            // Validate variant exists and get current price
            var variant = await _context.ProductVariants.FindAsync([productVariantId], cancellationToken);
            if (variant == null)
                return ApiResponse<CartResponse>.ErrorResponse("Product variant not found");

            // Determine reservation ID (customer or anonymous)
            string reservationId = customerId?.ToString() ?? anonymousId!;

            // Calculate new total quantity
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
            int newTotalQuantity = (existingItem?.Quantity ?? 0) + quantity;

            // Check stock availability and reserve
            try
            {
                bool reserved = await _inventoryService.ReserveStockAsync(
                    productVariantId, 
                    newTotalQuantity, 
                    reservationId, 
                    ReservationDuration, 
                    cancellationToken);

                if (!reserved)
                    return ApiResponse<CartResponse>.ErrorResponse("Insufficient stock available");
            }
            catch (Exception ex)
            {
                return ApiResponse<CartResponse>.ErrorResponse($"Stock reservation failed: {ex.Message}");
            }

            // Update cart items
            if (existingItem != null)
            {
                existingItem.Quantity = newTotalQuantity;
                existingItem.PriceAtAdd = variant.Price; // Update to latest price
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    Id = Guid.NewGuid(),
                    ProductVariantId = productVariantId,
                    Quantity = quantity,
                    PriceAtAdd = variant.Price
                });
            }

            // Save to Redis
            await _db.StringSetAsync(key, JsonSerializer.Serialize(cart, _jsonOptions), TimeSpan.FromSeconds(CartTtlSeconds));

            // Map and return response
            var response = await MapToResponseAsync(cart, key, cancellationToken);
            return ApiResponse<CartResponse>.SuccessResponse(response, "Item added to cart");
        }
        catch (RedisConnectionException)
        {
            return ApiResponse<CartResponse>.ErrorResponse("Cart service unavailable");
        }
    }

    public async Task<ApiResponse<CartResponse>> RemoveItemAsync(
        Guid? applicationUserId,
        string? anonymousId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);
        if (customerId == null && string.IsNullOrEmpty(anonymousId))
            return ApiResponse<CartResponse>.ErrorResponse("No cart identifier provided");

        var key = GetCartKey(customerId, anonymousId);

        try
        {
            var data = await _db.StringGetAsync(key);
            if (data.IsNullOrEmpty)
                return ApiResponse<CartResponse>.ErrorResponse("Cart is empty");

            Cart cart;
            try
            {
                cart = JsonSerializer.Deserialize<Cart>(data!, _jsonOptions)!;
            }
            catch (JsonException)
            {
                await _db.KeyDeleteAsync(key);
                cart = CreateNewCart(customerId, anonymousId);
            }

            var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
                return ApiResponse<CartResponse>.ErrorResponse("Item not found in cart");

            // Release stock reservation
            string reservationId = customerId?.ToString() ?? anonymousId!;
            try
            {
                await _inventoryService.ReleaseReservationAsync(item.ProductVariantId, reservationId, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log but don't fail the removal
                _context.Database.ExecuteSqlRaw($"-- Failed to release reservation: {ex.Message}");
            }

            cart.Items.Remove(item);

            await _db.StringSetAsync(key, JsonSerializer.Serialize(cart, _jsonOptions), TimeSpan.FromSeconds(CartTtlSeconds));

            var response = await MapToResponseAsync(cart, key, cancellationToken);
            return ApiResponse<CartResponse>.SuccessResponse(response, "Item removed");
        }
        catch (RedisConnectionException)
        {
            return ApiResponse<CartResponse>.ErrorResponse("Cart service unavailable");
        }
    }

    public async Task<ApiResponse<bool>> ClearCartAsync(
        Guid? applicationUserId,
        string? anonymousId,
        CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);
        if (customerId == null && string.IsNullOrEmpty(anonymousId))
            return ApiResponse<bool>.ErrorResponse("No cart identifier provided");

        var key = GetCartKey(customerId, anonymousId);

        try
        {
            // Fetch cart items before clearing to release reservations
            var data = await _db.StringGetAsync(key);
            if (!data.IsNullOrEmpty)
            {
                try
                {
                    var cart = JsonSerializer.Deserialize<Cart>(data!, _jsonOptions);
                    if (cart?.Items?.Any() == true)
                    {
                        string reservationId = customerId?.ToString() ?? anonymousId!;
                        
                        // Release all reservations
                        foreach (var item in cart.Items)
                        {
                            try
                            {
                                await _inventoryService.ReleaseReservationAsync(
                                    item.ProductVariantId, 
                                    reservationId, 
                                    cancellationToken);
                            }
                            catch
                            {
                                // Continue releasing others even if one fails
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Cart data corrupted, just delete it
                }
            }

            await _db.KeyDeleteAsync(key);
            return ApiResponse<bool>.SuccessResponse(true, "Cart cleared successfully");
        }
        catch (RedisConnectionException)
        {
            return ApiResponse<bool>.ErrorResponse("Cart service unavailable");
        }
    }

    public async Task<ApiResponse<bool>> TransferAnonymousCartToCustomerAsync(
        string anonymousId,
        Guid applicationUserId,
        CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);
        if (!customerId.HasValue)
            return ApiResponse<bool>.ErrorResponse("User has no customer profile");

        var anonKey = GetCartKey(null, anonymousId);
        var customerKey = GetCartKey(customerId, null);

        try
        {
            var anonData = await _db.StringGetAsync(anonKey);
            if (anonData.IsNullOrEmpty)
                return ApiResponse<bool>.SuccessResponse(true, "No anonymous cart to transfer");

            Cart anonCart = JsonSerializer.Deserialize<Cart>(anonData!, _jsonOptions)
                            ?? CreateNewCart(null, anonymousId);

            var custData = await _db.StringGetAsync(customerKey);
            Cart customerCart = custData.IsNullOrEmpty
                ? CreateNewCart(customerId, null)
                : JsonSerializer.Deserialize<Cart>(custData!, _jsonOptions) ?? CreateNewCart(customerId, null);

            // Merge
            foreach (var item in anonCart.Items)
            {
                var existing = customerCart.Items.FirstOrDefault(i => i.ProductVariantId == item.ProductVariantId);
                if (existing != null)
                    existing.Quantity += item.Quantity;
                else
                    customerCart.Items.Add(item);
            }

            await _db.StringSetAsync(customerKey, JsonSerializer.Serialize(customerCart, _jsonOptions), TimeSpan.FromSeconds(CartTtlSeconds));
            await _db.KeyDeleteAsync(anonKey);

            return ApiResponse<bool>.SuccessResponse(true, "Cart transferred successfully");
        }
        catch (RedisConnectionException)
        {
            return ApiResponse<bool>.ErrorResponse("Cart service unavailable");
        }
    }
    public async Task<ApiResponse<CartResponse>> ApplyCouponAsync(
        Guid? applicationUserId,
        string? anonymousId,
        string couponCode,
        CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);
        if (customerId == null && string.IsNullOrEmpty(anonymousId))
            return ApiResponse<CartResponse>.ErrorResponse("No cart identifier provided");

        var key = GetCartKey(customerId, anonymousId);

        try
        {
            var data = await _db.StringGetAsync(key);
            if (data.IsNullOrEmpty)
                 return ApiResponse<CartResponse>.ErrorResponse("Cart not found");

            Cart cart = JsonSerializer.Deserialize<Cart>(data!, _jsonOptions) ?? CreateNewCart(customerId, anonymousId);
            
            // Calculate subtotal for validation
            // Note: We need accurate subtotal. MapToResponse does this but we haven't called it yet.
            // Let's iterate items similar to GetCart logic or just use stored cart if it has cached subtotal (it doesn't seem to persist subtotal).
            // We need to fetch variants to get prices. This duplicates logic in MapToResponseAsync.
            // BETTER: Call GetCartAsync first? 
            // If we call GetCartAsync, it calculates totals. But it also applies EXISTING coupon. 
            // We want to apply NEW coupon.
            
            // Let's check subtotal roughly or fetch prices. 
            // For now, let's assume we can fetch the cart response w/o coupon to get subtotal.
            // But GetCartAsync will read "AppliedCouponCode" from Redis.
            // So we might need to calculate manually or temporarily rely on pre-check.
            
            // Simpler approach: MapToResponseAsync does the heavy lifting.
            // But MapToResponseAsync requires `variants` lookup.
            
            // Let's defer to CouponService validation. It needs 'orderSubtotal'.
            // Use MapToResponseAsync internally to get the object with totals.
            var tempResponse = await MapToResponseAsync(cart, key, cancellationToken);
            decimal currentSubtotal = tempResponse.Subtotal;
            
            try 
            {
               await _couponService.ValidateAndRegisterUsageAsync(couponCode, currentSubtotal, cancellationToken);
            }
            catch (Exception ex)
            {
                // If invalid, clear any existing coupon just in case?
                // Or simply return error.
                return ApiResponse<CartResponse>.ErrorResponse(ex.Message);
            }

            // Store coupon code in cart object if validation passed
            cart.AppliedCouponCode = couponCode.Trim().ToUpperInvariant();
            await _db.StringSetAsync(key, JsonSerializer.Serialize(cart, _jsonOptions), TimeSpan.FromSeconds(CartTtlSeconds));

            // Return updated cart with calculated discount
            var cartResponse = await GetCartAsync(applicationUserId, anonymousId, cancellationToken);
            return cartResponse;
        }
        catch (RedisConnectionException)
        {
            return ApiResponse<CartResponse>.ErrorResponse("Cart service unavailable");
        }
    }
}