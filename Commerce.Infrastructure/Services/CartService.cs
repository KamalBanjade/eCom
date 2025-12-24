using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Carts;
using Commerce.Domain.Entities.Carts;
using Commerce.Domain.Entities.Products;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly CommerceDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CartService(CommerceDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<Guid?> GetCustomerProfileIdAsync(Guid? applicationUserId)
    {
        if (!applicationUserId.HasValue) return null;
        
        var user = await _userManager.FindByIdAsync(applicationUserId.Value.ToString());
        return user?.CustomerProfileId;
    }

    public async Task<ApiResponse<Cart>> GetCartAsync(Guid? applicationUserId, string? anonymousId, CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);

        var cart = await GetCartEntityAsync(customerId, anonymousId, cancellationToken);
        if (cart == null)
        {
            // Create new cart
            cart = customerId.HasValue 
                ? Cart.CreateForCustomer(customerId.Value) 
                : Cart.CreateAnonymous(anonymousId!);
                
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }
        
        return ApiResponse<Cart>.SuccessResponse(cart);
    }

    public async Task<ApiResponse<Cart>> AddItemAsync(Guid? applicationUserId, string? anonymousId, Guid productVariantId, int quantity, CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);

        var cart = await GetCartEntityAsync(customerId, anonymousId, cancellationToken);
        if (cart == null)
        {
            cart = customerId.HasValue 
                ? Cart.CreateForCustomer(customerId.Value) 
                : Cart.CreateAnonymous(anonymousId!);
            _context.Carts.Add(cart);
        }

        var variant = await _context.ProductVariants.FindAsync(new object[] { productVariantId }, cancellationToken);
        if (variant == null)
        {
            return ApiResponse<Cart>.ErrorResponse("Product variant not found");
        }

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                ProductVariantId = productVariantId,
                Quantity = quantity,
                PriceAtAdd = variant.Price
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<Cart>.SuccessResponse(cart);
    }

    public async Task<ApiResponse<Cart>> RemoveItemAsync(Guid? applicationUserId, string? anonymousId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);

        var cart = await GetCartEntityAsync(customerId, anonymousId, cancellationToken);
        if (cart == null)
        {
            return ApiResponse<Cart>.ErrorResponse("Cart not found");
        }

        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            cart.Items.Remove(item);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ApiResponse<Cart>.SuccessResponse(cart);
    }

    public async Task<ApiResponse<bool>> TransferAnonymousCartToCustomerAsync(string anonymousId, Guid applicationUserId, CancellationToken cancellationToken = default)
    {
        var customerId = await GetCustomerProfileIdAsync(applicationUserId);
        if (!customerId.HasValue) return ApiResponse<bool>.ErrorResponse("User has no associated customer profile");

        var anonymousCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, cancellationToken);

        if (anonymousCart == null)
        {
            return ApiResponse<bool>.SuccessResponse(true, "No anonymous cart to transfer");
        }

        var customerCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerProfileId == customerId.Value, cancellationToken);

        if (customerCart == null)
        {
            // Simple transfer: just claim ownership
            anonymousCart.TransferToCustomer(customerId.Value);
        }
        else
        {
            // Merge items: add anonymous items to customer cart
            foreach (var item in anonymousCart.Items.ToList())
            {
                var existingItem = customerCart.Items
                    .FirstOrDefault(i => i.ProductVariantId == item.ProductVariantId);

                if (existingItem != null)
                {
                    existingItem.Quantity += item.Quantity;
                }
                else
                {
                    // Move item to new cart
                    item.CartId = customerCart.Id;
                    customerCart.Items.Add(item);
                }
            }
            // Remove old cart
            _context.Carts.Remove(anonymousCart);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Cart transferred successfully");
    }

    private async Task<Cart?> GetCartEntityAsync(Guid? customerId, string? anonymousId, CancellationToken cancellationToken)
    {
        IQueryable<Cart> query = _context.Carts.Include(c => c.Items).ThenInclude(i => i.ProductVariant);

        if (customerId.HasValue)
        {
            return await query.FirstOrDefaultAsync(c => c.CustomerProfileId == customerId.Value, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(anonymousId))
        {
            return await query.FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, cancellationToken);
        }
        
        return null;
    }
}
