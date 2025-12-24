using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Carts;
using Commerce.Domain.Entities.Carts;
using Commerce.Domain.Entities.Products;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly CommerceDbContext _context;

    public CartService(CommerceDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<Cart>> GetCartAsync(Guid? customerId, string? anonymousId, CancellationToken cancellationToken = default)
    {
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

    public async Task<ApiResponse<Cart>> AddItemAsync(Guid? customerId, string? anonymousId, Guid productVariantId, int quantity, CancellationToken cancellationToken = default)
    {
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

    public async Task<ApiResponse<Cart>> RemoveItemAsync(Guid? customerId, string? anonymousId, Guid itemId, CancellationToken cancellationToken = default)
    {
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

    public async Task<ApiResponse<bool>> TransferAnonymousCartToCustomerAsync(string anonymousId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var anonymousCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, cancellationToken);

        if (anonymousCart == null)
        {
            return ApiResponse<bool>.SuccessResponse(true, "No anonymous cart to transfer");
        }

        var customerCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerProfileId == customerId, cancellationToken);

        if (customerCart == null)
        {
            // Simple transfer: just claim ownership
            anonymousCart.TransferToCustomer(customerId);
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
