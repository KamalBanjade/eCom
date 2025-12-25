using System.Text.Json;
using Commerce.Application.Common.DTOs;     // ← This is the key one!
using Commerce.Domain.Entities.Carts;
using Commerce.Domain.Entities.Products;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Commerce.Application.Features.Carts;
using StackExchange.Redis;

namespace Commerce.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CommerceDbContext _context;
    private readonly IDatabase _db;
    private const int CartTtlSeconds = 86400; // 24 hours

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    public CartService(
        IConnectionMultiplexer redis,
        UserManager<ApplicationUser> userManager,
        CommerceDbContext context)
    {
        _redis = redis;
        _userManager = userManager;
        _context = context;
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
                subtotal += item.PriceAtAdd * item.Quantity;
                totalItems += item.Quantity;

                var variant = variants.GetValueOrDefault(item.ProductVariantId);

                responseItems.Add(new CartItemResponse
                {
                    ProductVariantId = item.ProductVariantId,
                    ProductName = variant?.Product?.Name ?? "Unknown Product",
                    VariantName = variant?.Attributes != null && variant.Attributes.Any()
                        ? string.Join(" - ", variant.Attributes.Values.Where(v => !string.IsNullOrEmpty(v)))
                        : variant?.SKU ?? string.Empty,
                    ImageUrl = variant?.ImageUrl ?? string.Empty,
                    UnitPrice = item.PriceAtAdd,
                    Quantity = item.Quantity
                });
            }
        }

        return new CartResponse
        {
            CartId = key,
            Subtotal = subtotal,
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

            // Update cart items
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
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
}