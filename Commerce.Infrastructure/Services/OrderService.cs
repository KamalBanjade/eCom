using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Application.Features.Carts;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly CommerceDbContext _context;
    private readonly ICartService _cartService;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderService(CommerceDbContext context, ICartService cartService, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _cartService = cartService;
        _userManager = userManager;
    }

    public async Task<OrderDto> CreateOrderAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // 1. Retrieve Cart
        // Note: Checkout is typically for authenticated users in this flow (ApplicationUser linked)
        // If we support guest checkout later, we'd need anonymousId passed here. 
        // For now, assume userId is present.
        
        var cartResponse = await _cartService.GetCartAsync(userId, null, cancellationToken);
        if (!cartResponse.Success || cartResponse.Data == null || !cartResponse.Data.Items.Any())
        {
            throw new InvalidOperationException("Cart is empty or invalid");
        }
        
        var cart = cartResponse.Data;
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user?.CustomerProfileId == null) throw new InvalidOperationException("User profile not found");

        // 2. Validate Inventory (Optional Placeholder)
        // CheckStock(cart.Items);

        // 3. Create Order
        var order = new Order
        {
            CustomerProfileId = user.CustomerProfileId.Value,
            OrderStatus = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = cart.Items.Select(ci => new OrderItem
            {
                ProductVariantId = ci.ProductVariantId,
                Quantity = ci.Quantity,
               UnitPrice = ci.UnitPrice
            }).ToList()
        };

        // Calc totals
        order.SubTotal = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        order.TaxAmount = order.SubTotal * 0.1m; // 10% tax placeholder
        order.TotalAmount = order.SubTotal + order.TaxAmount + order.ShippingAmount;
        
        // Generat Order Number
        order.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        
        // Add addresses (Placeholder - should come from request DTO or user profile)
        order.ShippingAddress = new Domain.ValueObjects.Address("123 Main St", "City", "State", "12345", "Country");
        order.BillingAddress = order.ShippingAddress;

        // 4. Save to DB
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        // 5. Clear Redis Cart
        // Even if this fails, Order is safe. We log locally if needed.
        await _cartService.ClearCartAsync(userId, null, cancellationToken);

        return MapToDto(order);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .AsQueryable();

        if (!isAdmin)
        {
            // Security check: Users can only see their own orders linked via CustomerProfile
            // Note: This requires resolving CustomerProfileId from UserId, assuming simplified link here
            var userProfile = await _context.CustomerProfiles
                .FirstOrDefaultAsync(p => p.ApplicationUserId == userId.ToString(), cancellationToken);
                
            if (userProfile == null) return null;
            
            query = query.Where(o => o.CustomerProfileId == userProfile.Id);
        }

        var order = await query.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order == null) return null;

        return MapToDto(order);
    }

    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userProfile = await _context.CustomerProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == userId.ToString(), cancellationToken);
            
        if (userProfile == null) return Enumerable.Empty<OrderDto>();

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerProfileId == userProfile.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto);
    }

    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null) throw new KeyNotFoundException("Order not found");

        order.OrderStatus = status;
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(order);
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                ProductName = "Product Info Placeholder" // would need join to get name
            }).ToList()
        };
    }
}
